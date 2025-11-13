using Microsoft.AspNetCore.Mvc;
using MailClient.API.Services;
using MailClient.API.Models;

namespace MailClient.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MailController : ControllerBase
{
    private readonly IMailService _mailService;

    public MailController(IMailService mailService)
    {
        _mailService = mailService;
    }

    [HttpGet("inbox/{accountEmail}")]
    public async Task<ActionResult<List<EmailMessage>>> GetInboxEmails(string accountEmail)
    {
        try
        {
            var emails = await _mailService.GetInboxEmailsAsync(accountEmail);
            return Ok(emails);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("sent/{accountEmail}")]
    public async Task<ActionResult<List<EmailMessage>>> GetSentEmails(string accountEmail)
    {
        try
        {
            var emails = await _mailService.GetSentEmailsAsync(accountEmail);
            return Ok(emails);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{accountEmail}/{emailId}")]
    public async Task<ActionResult<EmailMessage>> GetEmailById(string accountEmail, string emailId)
    {
        try
        {
            var email = await _mailService.GetEmailByIdAsync(accountEmail, emailId);
            if (email == null)
                return NotFound();
            return Ok(email);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}


