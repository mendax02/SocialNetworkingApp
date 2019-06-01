using System.Collections.Generic;
using System.Threading.Tasks;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private readonly DataContext _datacontext;

        public DatingRepository(DataContext datacontext)
        {
            this._datacontext = datacontext;

        }
        public void Add<T>(T entity) where T : class
        {
            _datacontext.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
           _datacontext.Remove(entity);
        }

        public  async Task<User> GetUser(int id)
        {
           var user = await _datacontext.Users.Include(p=> p.Photos).FirstOrDefaultAsync(u=>u.Id == id);
           return user;
        }

        public async Task<IEnumerable<User>> GetUsers()
        {
           var users = await _datacontext.Users.Include(p => p.Photos).ToListAsync();

           return users;
        }

        public async Task<bool> SaveAll()
        {
           return await _datacontext.SaveChangesAsync() > 0;
        }
    }
}