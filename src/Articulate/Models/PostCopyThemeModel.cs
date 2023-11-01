using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Umbraco.Extensions;

namespace Articulate.Models
{
    public class PostCopyThemeModel : IValidatableObject
    {
        [Required(AllowEmptyStrings = false)]
        public string ThemeName { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string NewThemeName { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Path.GetInvalidFileNameChars().ContainsAny(NewThemeName.ToCharArray()))
            {
                yield return new ValidationResult("Name cannot contain invalid file name characters", new[] { nameof(ThemeName) });
            }
        }
    }
}
