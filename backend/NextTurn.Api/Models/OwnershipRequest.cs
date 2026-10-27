using System.ComponentModel.DataAnnotations;

namespace NextTurn.Api.Models;

public class OwnershipRequest
{
    [Required]
    public bool? IsOwned { get; set; }
}
