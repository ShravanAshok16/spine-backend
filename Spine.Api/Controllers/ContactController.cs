using Microsoft.AspNetCore.Mvc;
using Spine.Api.Events;
using Spine.Api.Models;
using Spine.Api.Services;

namespace Spine.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly KafkaProducerService _kafkaProducer;

    public ContactController(KafkaProducerService kafkaProducer)
    {
        _kafkaProducer = kafkaProducer;
    }

    [HttpPost]
    public async Task<IActionResult> SendContactMessage([FromBody] ContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email and message are required." });

        var contactEvent = new ContactFormSubmittedEvent
        {
            Name = request.Name,
            Email = request.Email,
            Message = request.Message
        };

        await _kafkaProducer.PublishContactFormEventAsync(contactEvent);

        return Ok(new { message = "Message sent successfully." });
    }
}