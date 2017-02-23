using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotHook
{
    public static class Magic<T>
    {
        public static void Return(T t)
        {
            throw new InvalidOperationException("Magic method called!");
        }
    }
}
