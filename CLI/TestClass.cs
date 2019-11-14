using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BuilderCreator.CLI
{
    public class TestClass
    {
        public int Foo { get; set; }
        public string Bar { get; }

#pragma warning disable IDE0044,IDE0032,0649 // Add readonly modifier
        private float _baz;
#pragma warning restore IDE0044,IDE0032,0649 // Add readonly modifier
        public float Baz
        {
            get { return _baz; }
        }

        private double Qux { get; set; }

        public TestClass()
        {

        }

        public double GetQux()
        {
            return Qux;
        }
    }
}
