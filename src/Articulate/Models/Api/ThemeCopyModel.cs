#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Articulate.Models.Api
{
    /// <summary>
    ///     Represents the data required to copy an existing theme to a new theme name.
    ///     The new theme name must not contain any invalid file name characters.
    /// </summary>
    public class ThemeCopyModel : IValidatableObject
    {
        /// <summary>
        ///     Gets or sets the name of the existing theme to copy.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public required string ThemeName { get; set; }

        /// <summary>
        ///     Gets or sets the new name for the copied theme.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public required string NewThemeName { get; set; }

        /// <summary>
        ///     Validates that the new theme name does not contain invalid file name characters.
        /// </summary>
        /// <param name="validationContext">The context in which the validation is performed.</param>
        /// <returns>A collection of validation results.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrWhiteSpace(NewThemeName) &&
                NewThemeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                yield return new ValidationResult(
                    "The theme name contains invalid characters.",
                    [nameof(NewThemeName)]);
            }
        }
    }
}
