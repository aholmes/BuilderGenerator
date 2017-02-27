# BuilderGenerator

Generates a "builder" class based on the properties of a selected class.

The specific use case may not be useful to anyone outside of a certain organization, but the code does demonstrate generating a complex class with Roslyn.

# VSIX Usage

After compilation, under `Builder/(Debug|Release)/bin/`, you will fine the Builder.VSIX.vsix file.
Execute this file to install the extension.

After the extension is installed, you can right-click a C# file in Visual Studio, then click Invoke Builder, to create a new Builder class.

# Command-line Usage

	Program.exe path\to\ClassFile.cs ClassName

# Example

## Input

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

## Output

    public sealed class TestClassBuilder : EntityBuilder<TestClassBuilder, TestClass>
    {
        protected BuilderProperty<int> Foo
        {
            get
            {
                return _foo;
            }
        }

        protected BuilderProperty<string> Bar
        {
            get
            {
                return _bar;
            }
        }

        protected BuilderProperty<float> Baz
        {
            get
            {
                return _baz;
            }
        }

        private readonly BuilderProperty<int> _foo;
        private readonly BuilderProperty<string> _bar;
        private readonly BuilderProperty<float> _baz;
        public TestClassBuilder()
        {
            _foo = new BuilderProperty<int>(this);
            _bar = new BuilderProperty<string>(this);
            _baz = new BuilderProperty<float>(this);
        }

        public TestClassBuilder WithFoo(int foo)
        {
            Foo.SetValue(foo);
            return ThisBuilder;
        }

        public TestClassBuilder WithBar(string bar)
        {
            Bar.SetValue(bar);
            return ThisBuilder;
        }

        public TestClassBuilder WithBaz(float baz)
        {
            Baz.SetValue(baz);
            return ThisBuilder;
        }

        public override TestClass Build()
        {
            var testclass = new TestClass();
            if (Foo.HasValueOrAutoData)
            {
                testclass.Foo.Value = Foo;
            }

            if (Bar.HasValueOrAutoData)
            {
                testclass.Bar.Value = Bar;
            }

            if (Baz.HasValueOrAutoData)
            {
                testclass.Baz.Value = Baz;
            }

            return testclass;
        }
    }
