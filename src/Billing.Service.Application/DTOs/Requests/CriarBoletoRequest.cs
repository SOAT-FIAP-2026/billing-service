namespace Billing.Service.Application.DTOs.Requests;

public sealed record CriarBoletoRequest(
    Guid IdOrcamento,
    string Email,
    string Nome,
    string Sobrenome,
    string Cpf);
