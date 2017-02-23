using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Target
{
    class Program
    {
        static void Main(string[] args)
        {
            var foo = new Target(10);
            Console.WriteLine(foo.Add(2));
        }

        static int Add(int a, int b)
        {
            return a + b;
        }
    }

    public class Target
    {
        public int m_a;
        public Target(int a)
        {
            m_a = a;
        }
        int A
        {
            get
            {
                return m_a;
            }
        }
        public int Add(int b)
        {
            return m_a + b;
        }
    }
}
