using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Runtime.Validation;

namespace Abp.Dependency.CompileTime
{
    public static class CompileTimeDtoValidator
    {
        public static void ValidateArguments(params object?[] arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument == null)
                {
                    continue;
                }

                var results = new List<ValidationResult>();
                var context = new ValidationContext(argument);
                if (!Validator.TryValidateObject(argument, context, results, validateAllProperties: true))
                {
                    throw new AbpValidationException("Validation failed.", results);
                }
            }
        }
    }
}
