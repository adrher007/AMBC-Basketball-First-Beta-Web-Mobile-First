using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

public class NotificationCreateDto
{
    [Required]
    public string Mensaje { get; set; } = "";
    [Required]
    public string Destinatarios { get; set; } = ""; // ALL | COACH | FAN | ARBITRO | TEAM:<id> | USER:<id>
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Mensaje { get; set; } = "";
    public string Destinatarios { get; set; } = "";
    public DateTime CreadoEn { get; set; }
    public bool Vista { get; set; }
}
