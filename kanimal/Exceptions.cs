using System;

namespace kanimal
{
    public enum ExitCodes
    {
        Normal = 0,
        GenericProblem = 1,
        IncorrectHeader = 2,
        IncorrectArguments = 3,
    }

    public class KAnimalException : Exception
    {
        protected KAnimalException(string message)
            : base(message)
        {
        }
    }

    public class HeaderAssertException : KAnimalException
    {
        public string ExpectedHeader { get; }
        public string ActualHeader { get; }

        public HeaderAssertException(string message, string expected, string actual)
            : base(message)
        {
            ExpectedHeader = expected;
            ActualHeader = actual;
        }
    }

    public class SpriteParseException : KAnimalException
    {
        public string Filename { get; }

        public SpriteParseException(string message, string filename)
            : base(message)
        {
            Filename = filename;
        }
    }

    public class ProjectParseException : KAnimalException
    {
        public ProjectParseException(string message)
            : base(message)
        {
        }
    }
}