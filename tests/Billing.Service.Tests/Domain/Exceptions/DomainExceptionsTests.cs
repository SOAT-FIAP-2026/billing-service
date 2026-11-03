using Billing.Service.Domain.Exceptions;

namespace Billing.Service.Tests.Domain.Exceptions
{
    public class DomainExceptionsTests
    {
        [Fact]
        public void OrdemServicoNaoEncontradaException_DeveHerdaDomainExceptionEDefinirMensagem()
        {
            var id = Guid.NewGuid();

            var exception = new OrdemServicoNaoEncontradaException(id);

            Assert.IsAssignableFrom<DomainException>(exception);
            Assert.Equal($"Ordem de serviço com id '{id}' não encontrada.", exception.Message);
        }

        [Fact]
        public void PecaInsumoNaoEncontradaException_DeveHerdaDomainExceptionEDefinirMensagem()
        {
            var id = Guid.NewGuid();

            var exception = new PecaInsumoNaoEncontradaException(id);

            Assert.IsAssignableFrom<DomainException>(exception);
            Assert.Equal($"Peça/Insumo com id '{id}' não encontrado.", exception.Message);
        }

        [Fact]
        public void StatusOrdemServicoNaoEncontradoException_DeveHerdaDomainExceptionEDefinirMensagem()
        {
            const string descricao = "Finalizada";

            var exception = new StatusOrdemServicoNaoEncontradoException(descricao);

            Assert.IsAssignableFrom<DomainException>(exception);
            Assert.Equal($"Status de ordem de serviço '{descricao}' não encontrado.", exception.Message);
        }
    }
}