using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Infrastructure.Saving
{
    public interface IRepository<T> where T : class
    {
        T Load();
        void Save(T entity);
        void Clear();
        void Backup();
    }
}
