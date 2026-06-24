using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Abp.Collections.Extensions;
using Abp.Configuration.Startup;
using Abp.Dependency;
using Abp.Reflection;

namespace Abp.Runtime.Validation.Interception
{
    /// <summary>
    /// Reflection-based method argument validator for Castle DynamicProxy.
    /// Moved from <c>src/Abp/Runtime/Validation/Interception/MethodInvocationValidator.cs</c>.
    /// </summary>
    public class MethodInvocationValidator : IMethodInvocationValidator
    {
        private const int MaxRecursiveParameterValidationDepth = 8;

        protected MethodInfo Method { get; private set; }
        protected object[] ParameterValues { get; private set; }
        protected ParameterInfo[] Parameters { get; private set; }
        protected List<ValidationResult> ValidationErrors { get; }
        protected List<IShouldNormalize> ObjectsToBeNormalized { get; }

        private readonly IValidationConfiguration _configuration;
        private readonly IIocResolver _iocResolver;

        public MethodInvocationValidator(IValidationConfiguration configuration, IIocResolver iocResolver)
        {
            _configuration = configuration;
            _iocResolver = iocResolver;

            ValidationErrors = new List<ValidationResult>();
            ObjectsToBeNormalized = new List<IShouldNormalize>();
        }

        public virtual void Initialize(MethodInfo method, object[] parameterValues)
        {
            Check.NotNull(method, nameof(method));
            Check.NotNull(parameterValues, nameof(parameterValues));

            Method = method;
            ParameterValues = parameterValues;
            Parameters = method.GetParameters();
        }

        public virtual void Initialize(AbpMethodInfo method, object[] parameterValues)
        {
            Check.NotNull(method, nameof(method));
            Check.NotNull(parameterValues, nameof(parameterValues));

            if (method.ReflectionMethod != null)
            {
                Initialize(method.ReflectionMethod, parameterValues);
            }
        }

        public void Validate()
        {
            CheckInitialized();

            if (Parameters.IsNullOrEmpty())
            {
                return;
            }

            if (!Method.IsPublic)
            {
                return;
            }

            if (IsValidationDisabled())
            {
                return;
            }

            if (Parameters.Length != ParameterValues.Length)
            {
                throw new Exception("Method parameter count does not match with argument count!");
            }

            for (var i = 0; i < Parameters.Length; i++)
            {
                ValidateMethodParameter(Parameters[i], ParameterValues[i]);
            }

            if (ValidationErrors.Any())
            {
                ThrowValidationError();
            }

            foreach (var objectToBeNormalized in ObjectsToBeNormalized)
            {
                objectToBeNormalized.Normalize();
            }
        }

        protected virtual void CheckInitialized()
        {
            if (Method == null)
            {
                throw new AbpException("This object has not been initialized. Call Initialize method first.");
            }
        }

        protected virtual bool IsValidationDisabled()
        {
            if (Method.IsDefined(typeof(EnableValidationAttribute), true))
            {
                return false;
            }

            return ReflectionHelper.GetSingleAttributeOfMemberOrDeclaringTypeOrDefault<DisableValidationAttribute>(Method) != null;
        }

        protected virtual void ThrowValidationError()
        {
            throw new AbpValidationException(
                "Method arguments are not valid! See ValidationErrors for details.",
                ValidationErrors
            );
        }

        protected virtual void ValidateMethodParameter(ParameterInfo parameterInfo, object parameterValue)
        {
            if (parameterValue == null)
            {
                if (!parameterInfo.IsOptional &&
                    !parameterInfo.IsOut &&
                    !TypeHelper.IsPrimitiveExtendedIncludingNullable(parameterInfo.ParameterType, includeEnums: true))
                {
                    ValidationErrors.Add(new ValidationResult(parameterInfo.Name + " is null!", new[] { parameterInfo.Name }));
                }

                return;
            }

            ValidateObjectRecursively(parameterValue, 1);
        }

        protected virtual void ValidateObjectRecursively(object validatingObject, int currentDepth)
        {
            if (currentDepth > MaxRecursiveParameterValidationDepth)
            {
                return;
            }

            if (validatingObject == null)
            {
                return;
            }

            if (_configuration.IgnoredTypes.Any(t => t.IsInstanceOfType(validatingObject)))
            {
                return;
            }

            if (TypeHelper.IsPrimitiveExtendedIncludingNullable(validatingObject.GetType()))
            {
                return;
            }

            SetValidationErrors(validatingObject);

            if (IsEnumerable(validatingObject))
            {
                foreach (var item in (IEnumerable)validatingObject)
                {
                    if (item == null || TypeHelper.IsPrimitiveExtendedIncludingNullable(item.GetType()))
                    {
                        break;
                    }

                    ValidateObjectRecursively(item, currentDepth + 1);
                }
            }

            if (validatingObject is IShouldNormalize)
            {
                ObjectsToBeNormalized.Add(validatingObject as IShouldNormalize);
            }

            if (ShouldMakeDeepValidation(validatingObject))
            {
                var properties = TypeDescriptor.GetProperties(validatingObject).Cast<PropertyDescriptor>();
                foreach (var property in properties)
                {
                    if (property.Attributes.OfType<DisableValidationAttribute>().Any())
                    {
                        continue;
                    }

                    ValidateObjectRecursively(property.GetValue(validatingObject), currentDepth + 1);
                }
            }
        }

        protected virtual void SetValidationErrors(object validatingObject)
        {
            foreach (var validatorType in _configuration.Validators)
            {
                if (ShouldValidateUsingValidator(validatingObject, validatorType))
                {
                    using (var validator = _iocResolver.ResolveAsDisposable<IMethodParameterValidator>(validatorType))
                    {
                        var validationResults = validator.Object.Validate(validatingObject);
                        ValidationErrors.AddRange(validationResults);
                    }
                }
            }
        }

        protected virtual bool ShouldValidateUsingValidator(object validatingObject, Type validatorType)
        {
            return true;
        }

        protected virtual bool ShouldMakeDeepValidation(object validatingObject)
        {
            if (validatingObject is IEnumerable)
            {
                return false;
            }

            var validatingObjectType = validatingObject.GetType();

            if (TypeHelper.IsPrimitiveExtendedIncludingNullable(validatingObjectType))
            {
                return false;
            }

            return true;
        }

        private bool IsEnumerable(object validatingObject)
        {
            return
                validatingObject is IEnumerable &&
                !(validatingObject is IQueryable) &&
                !TypeHelper.IsPrimitiveExtendedIncludingNullable(validatingObject.GetType());
        }
    }
}
