using System;
using MikroTikVoucherPrinter.Domain.Enums;

namespace MikroTikVoucherPrinter.Application.DTOs;

public class VoucherGenerationRequest
{
    public GenerationMode Mode { get; set; } = GenerationMode.Bulk;
    public int Count { get; set; }
    
    public string Prefix { get; set; } = string.Empty;
    public int UsernameLength { get; set; } = 9;
    
    public CredentialMode CredentialMode { get; set; } = CredentialMode.UsernameOnly;
    public CharacterMode CharacterMode { get; set; } = CharacterMode.DigitsOnly;
    
    public string PasswordPrefix { get; set; } = string.Empty;
    public int PasswordLength { get; set; } = 6;
    public CharacterMode PasswordCharacterMode { get; set; } = CharacterMode.DigitsOnly;
    
    public string SingleUsername { get; set; } = string.Empty;
    public string SinglePassword { get; set; } = string.Empty;

    public string ProfileName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid? AgentId { get; set; }
    
    public bool AutoPrint { get; set; }
    public bool AutoSync { get; set; }
    public Guid? PrintTemplateId { get; set; }
}
