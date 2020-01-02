using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BTCPayServer.Lightning;
using BTCPayServer.Rating;

namespace BTCPayServer.Validation
{
    public class RateScriptValidator : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            if (RateRules.TryParse(value.ToString(), out _, out var errors)) return ValidationResult.Success;
            errors = errors ?? new List<RateRulesErrors>();
            var errorString = string.Join(", ", errors.ToArray());
            return new ValidationResult($"Parsing error ({errorString})");
        }
    }
}
