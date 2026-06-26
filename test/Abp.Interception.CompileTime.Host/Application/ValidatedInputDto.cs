using System.ComponentModel.DataAnnotations;

namespace Abp.Interception.CompileTime.Host.Application;

public sealed class ValidatedInputDto
{
    [Required]
    public string Name { get; set; } = "";
}
