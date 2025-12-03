using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RideShare_Connect.Attributes
{
    public class ValidDrivingExperienceAttribute : ValidationAttribute
    {
        private readonly string _dobPropertyName;

        public ValidDrivingExperienceAttribute(string dobPropertyName)
        {
            _dobPropertyName = dobPropertyName;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var dobProperty = validationContext.ObjectType.GetProperty(_dobPropertyName);
            if (dobProperty == null)
            {
                return new ValidationResult($"Unknown property: {_dobPropertyName}");
            }

            var dobValue = dobProperty.GetValue(validationContext.ObjectInstance);
            if (dobValue is DateTime dob && value is int experience)
            {
                var age = DateTime.Today.Year - dob.Year;
                if (dob.Date > DateTime.Today.AddYears(-age)) age--;

                var maxExperience = age - 18;

                if (experience > maxExperience)
                {
                    return new ValidationResult($"Driving experience cannot be greater than {maxExperience} years (Age - 18).");
                }
            }
            return ValidationResult.Success;
        }
    }
}
