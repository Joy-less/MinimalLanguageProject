using HoloLang;

List<Expression> Expressions = Parser.Parse("""
    "hello there";
    4.6;

    {
        a;
    }
    """).Value;

_ = Expressions;