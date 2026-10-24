using Billing.Service.Domain.Entities;
using Billing.Service.Domain.Interfaces.Repository;
using Billing.Service.Domain.ValueObjects;
using Billing.Service.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Service.Infrastructure.Repositories
{
    public class UsuarioRepository(ApplicationDbContext context) : IUsuarioRepository
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Usuario?> ObterPorLogin(EmailVO email)
        {
        
            return await _context.Usuarios
                .Include(u => u.Permissao)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<bool> ExisteEmail(EmailVO email)
        {
            return await _context.Usuarios
                .AnyAsync(u => u.Email == email);
        }

        public async Task Adicionar(Usuario usuario)
        {
            await _context.Usuarios.AddAsync(usuario);
            await _context.SaveChangesAsync();
        }
    }
}
