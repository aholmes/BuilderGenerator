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

        private float _baz;
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
