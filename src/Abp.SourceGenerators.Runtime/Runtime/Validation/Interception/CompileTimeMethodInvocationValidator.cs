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
    /// Compile-time method argument validator using <see cref="AbpMethodInfo"/> metadata.
    /// Moved from <c>src/Abp/Runtime/Validation/Interception/MethodInvocationValidator.CompileTime.cs</c>.
    /// </summary>
    public class CompileTimeMethodInvocationValidator : IMethodInvocationValidator
    {
        private const int MaxRecursiveParameterValidationDepth = 8;

        protected MethodInfo Method { get; private set; }
        protected object[] ParameterValues { get; private set; }
        protected ParameterInfo[] Parameters { get; private set; }
        protected List<ValidationResult> ValidationErrors { get; }
        protected List<IShouldNormalize> ObjectsToBeNormalized { get; }

        private readonly IValidationConfiguration _configuration;
        private readonly IIocResolver _iocResolver;
        private AbpMethodInfo? _abpMethod;

        public CompileTimeMethodInvocationValidator(IValidationConfiguration configuration, IIocResolver iocResolver)
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

            _abpMethod = null;
            Method = method;
            ParameterValues = parameterValues;
            Parameters = method.GetParameters();
        }

        public virtual void Initialize(AbpMethodInfo method, object[] parameterValues)
        {
            Check.NotNull(method, nameof(method));
            Check.NotNull(parameterValues, nameof(parameterValues));

            _abpMethod = method;

            if (method.ReflectionMethod != null)
            {
                Initialize(method.ReflectionMethod, parameterValues);
                return;
            }

            Method = null!;
            ParameterValues = parameterValues;
            Parameters = Array.Empty<ParameterInfo>();
        }

        public void Validate()
        {
            CheckInitialized();

            if (TrySkipCompileTimeValidation())
            {
                return;
            }

            if (Parameters.IsNullOrEmpty())
            {
                if (_abpMethod != null)
                {
                    if (!_abpMethod.IsPublic || IsValidationDisabled())
                    {
                        return;
                    }

                    ValidateCompileTimeParameters();
                }

                if (ValidationErrors.Any())
                {
                    ThrowValidationError();
                }

                foreach (var objectToBeNormalized in ObjectsToBeNormalized)
                {
                    objectToBeNormalized.Normalize();
                }

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
            if (Method == null && _abpMethod == null)
            {
                throw new AbpException("This object has not been initialized. Call Initialize method first.");
            }
        }

        protected virtual bool IsValidationDisabled()
        {
            if (_abpMethod != null && Method == null)
            {
                if (_abpMethod.IsDefined(typeof(EnableValidationAttribute), true))
                {
                    return false;
                }

                return _abpMethod.GetCustomAttributes<DisableValidationAttribute>(true).Any();
            }

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

        private bool TrySkipCompileTimeValidation()
        {
            if (_abpMethod == null || Method != null)
            {
                return false;
            }

            if (_abpMethod is not IAbpBuiltInInterceptionMetadata builtIn
                || !builtIn.ShouldValidate
                || ParameterValues.Length == 0)
            {
                return true;
            }

            return false;
        }

        private void ValidateCompileTimeParameters()
        {
            if (_abpMethod == null || _abpMethod.ReflectionMethod != null)
            {
                return;
            }

            if (_abpMethod.Parameters.Count != ParameterValues.Length)
            {
                throw new Exception("Method parameter count does not match with argument count!");
            }

            for (var i = 0; i < _abpMethod.Parameters.Count; i++)
            {
                ValidateAbpParameter(_abpMethod.Parameters[i], ParameterValues[i]);
            }
        }

        private void ValidateAbpParameter(AbpParameterInfo parameter, object parameterValue)
        {
            if (parameterValue == null)
            {
                if (!parameter.IsOptional &&
                    !TypeHelper.IsPrimitiveExtendedIncludingNullable(parameter.ParameterType, includeEnums: true))
                {
                    ValidationErrors.Add(new ValidationResult(
                        parameter.Name + " is null!",
                        new[] { parameter.Name }));
                }

                return;
            }

            ValidateObjectRecursively(parameterValue, 1);
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
