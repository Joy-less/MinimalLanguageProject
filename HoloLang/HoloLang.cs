using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace HoloLang;

/*public static class Result {
    public static Success Success() {
        return new Success();
    }
    public static ResultValue<T> FromValue<T>(T Value) {
        return new ResultValue<T>(Value);
    }
    public static ResultError<E> FromError<E>(E Error) {
        return new ResultError<E>(Error);
    }
}*/
public readonly struct Success {
}
/*public readonly struct ResultValue<T> {
    public T Value { get; }

    public ResultValue(T Value) {
        this.Value = Value;
    }
}
public readonly struct ResultError<E> {
    public E Error { get; }

    public ResultError(E Error) {
        this.Error = Error;
    }
}*/
public readonly struct Result<T, E> {
    public T? ValueOrDefault { get; }
    [MemberNotNullWhen(true, nameof(ErrorOrDefault))]
    public bool IsError { get; }
    public E? ErrorOrDefault { get; }

    [MemberNotNullWhen(true, nameof(ValueOrDefault))]
    public bool IsValue => !IsError;
    public T Value => IsValue ? ValueOrDefault : throw new InvalidOperationException($"Result was error: {ErrorOrDefault}");
    public E Error => IsError ? ErrorOrDefault : throw new InvalidOperationException($"Result was value: {ValueOrDefault}");

    private Result(bool IsError, E? ErrorOrDefault, T? ValueOrDefault) {
        this.IsError = IsError;
        this.ErrorOrDefault = ErrorOrDefault;
        this.ValueOrDefault = ValueOrDefault;
    }

    public override string ToString() {
        return IsError
            ? $"Error: {ErrorOrDefault}"
            : $"Value: {ValueOrDefault}";
    }
    public void ThrowIfError() {
        if (IsError) {
            throw (ErrorOrDefault as Exception) ?? new Exception(ErrorOrDefault.ToString());
        }
    }

    public static Result<Success, E> FromSuccess() {
        return new Result<Success, E>(false, default, default);
    }
    public static Result<T, E> FromValue(T Value) {
        return new Result<T, E>(false, default, Value);
    }
    public static Result<T, E> FromError(E Error) {
        return new Result<T, E>(true, Error, default);
    }

    /*public bool TryGetValue([NotNullWhen(true)] out T? Value, [NotNullWhen(false)] out E? Error) {
        Value = ValueOrDefault;
        Error = ErrorOrDefault;
        return IsValue;
    }*/

    public static implicit operator Result<T, E>(Success ResultSuccess) {
        _ = ResultSuccess;
        return new Result<T, E>(true, default, default);
    }
    /*public static implicit operator Result<T, E>(ResultValue<T> ResultValue) {
        return new Result<T, E>(true, default, ResultValue.Value);
    }
    public static implicit operator Result<T, E>(ResultError<E> ResultError) {
        return new Result<T, E>(true, ResultError.Error, default);
    }*/
    /*public static implicit operator Result<T, E>(T Value) {
        return new Result<T, E>(false, default, Value);
    }*/
}

public sealed class Parser {
    public string Source { get; }
    public int Index { get; private set; }

    private static readonly char[] NewlineChars = ['\n', '\r'];
    private static readonly char[] WhitespaceChars = [' ', '\t', '\v', '\f', ..NewlineChars];

    private Parser(string Source) {
        this.Source = Source;
        Index = 0;
    }

    public static Result<Expression, string> Parse(string Source) {
        Parser Parser = new(Source);

        Result<Expression, string> ExpressionResult = Parser.ParseExpression();
        if (ExpressionResult.IsError) {
            return Result<Expression, string>.FromError(ExpressionResult.Error);
        }

        Result<Success, string> EndOfInputResult = Parser.ParseEndOfInput();
        if (EndOfInputResult.IsError) {
            return Result<Expression, string>.FromError(EndOfInputResult.Error);
        }

        return Result<Expression, string>.FromValue(ExpressionResult.Value);
    }

    private Result<Success, string> ParseEndOfInput() {
        // Whitespace
        ReadWhitespace();

        // Invalid
        if (Index < Source.Length) {
            return Result<Success, string>.FromError($"Expected `;`, got `{Source[Index]}`");
        }
        return Result<Success, string>.FromSuccess();
    }
    private Result<Expression, string> ParseExpression() {
        List<Expression> Expressions = [];

        for (; Index < Source.Length; Index++) {
            // Whitespace
            ReadWhitespace();

            // End of input
            if (Index >= Source.Length) {
                break;
            }

            // String
            if (Source[Index] is '"' or '\'') {
                // Consume string
                int StringStartIndex = Index;
                Result<Success, string> StringResult = ReadString();
                if (StringResult.IsError) {
                    return Result<Expression, string>.FromError(StringResult.Error);
                }
                ReadOnlySpan<char> String = Source.AsSpan(StringStartIndex..Index);

                // Create string expression
                Expressions.Add(new StringExpression(Encoding.UTF8.GetBytes(new string(String)))); // TODO: improve performance of (ReadOnlySpan<char> -> IEnumerable<byte>)
            }
            // Number
            else if (Source[Index] is (>= '0' and <= '9') or '-' or '+') {
                // Consume number
                int NumberStartIndex = Index;
                Result<Success, string> NumberResult = ReadNumber();
                if (NumberResult.IsError) {
                    return Result<Expression, string>.FromError(NumberResult.Error);
                }
                ReadOnlySpan<char> Number = Source.AsSpan(NumberStartIndex..Index);

                // Create real expression
                if (Number.Contains('.')) {
                    Expressions.Add(new RealExpression(double.Parse(Number)));
                }
                // Create integer expression
                else {
                    Expressions.Add(new IntegerExpression(long.Parse(Number)));
                }
            }
            // Identifier
            else if (Source[Index] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_') {
                // Consume identifier
                int IdentifierStartIndex = Index;
                Result<Success, string> IdentifierResult = ReadIdentifier();
                if (IdentifierResult.IsError) {
                    return Result<Expression, string>.FromError(IdentifierResult.Error);
                }
                ReadOnlySpan<char> Identifier = Source.AsSpan(IdentifierStartIndex..Index);

                // Consume whitespace
                ReadWhitespace();

                // Assignment
                if (Source[Index] is '=') {
                    Index++;

                    // Consume expression
                    Result<Expression, string> ValueResult = ParseExpression();
                    if (ValueResult.IsError) {
                        return Result<Expression, string>.FromError(ValueResult.Error);
                    }

                    // Create assign expression
                    Expressions.Add(new AssignExpression(null, new string(Identifier), ValueResult.Value));
                }
                else {
                    // Create get expression
                    Expressions.Add(new GetExpression(null, new string(Identifier)));
                }
            }
            // Box
            else if (Source[Index] is '{') {
                // Consume box expression
                Result<BoxExpression, string> BoxResult = ParseBox();
                if (BoxResult.IsError) {
                    return Result<Expression, string>.FromError(BoxResult.Error);
                }
                BoxExpression BoxExpression = BoxResult.Value;

                // Add box expression
                Expressions.Add(BoxExpression);
            }
            // Tuple
            else if (Source[Index] is '(') {
                // Consume tuple
                Result<List<Expression>, string> TupleResult = ParseTuple();
                if (TupleResult.IsError) {
                    return Result<Expression, string>.FromError(TupleResult.Error);
                }
                List<Expression> Tuple = TupleResult.Value;

                // Whitespace
                ReadWhitespace();

                // Box
                if (Index < Source.Length && Source[Index] is '{') {
                    // Consume box expression
                    Result<BoxExpression, string> BoxResult = ParseBox();
                    if (BoxResult.IsError) {
                        return Result<Expression, string>.FromError(BoxResult.Error);
                    }
                    BoxExpression BoxExpression = BoxResult.Value;

                    // Get parameters
                    List<string> Parameters = new(Tuple.Count);
                    foreach (Expression ParameterExpression in Tuple) {
                        if (ParameterExpression is not GetExpression ParameterGetExpression) {
                            return Result<Expression, string>.FromError($"Expected parameter, got `{ParameterExpression.GetType()}`");
                        }
                        if (ParameterGetExpression.Target is not null) {
                            return Result<Expression, string>.FromError($"Expected parameter, got targeted {nameof(GetExpression)}");
                        }
                        Parameters.Add(ParameterGetExpression.Member);
                    }

                    // Set box expression method
                    BoxExpression.Expression = null;
                    BoxExpression.Method = new BoxMethod(Parameters, BoxExpression);
                }
            }

            // Whitespace
            ReadWhitespace();

            // End of input
            if (Index >= Source.Length) {
                break;
            }

            // Semicolon
            if (Source[Index] is not ';') {
                break;
            }
        }

        if (Expressions.Count == 1) {
            return Result<Expression, string>.FromValue(Expressions[0]);
        }
        return Result<Expression, string>.FromValue(new MultiExpression(Expressions));
    }
    private Result<BoxExpression, string> ParseBox() {
        if (Source[Index] is not '{') {
            return Result<BoxExpression, string>.FromError("Expected `{` to start box");
        }
        Index++;

        Result<Expression, string> ExpressionsResult = ParseExpression();
        if (ExpressionsResult.IsError) {
            return Result<BoxExpression, string>.FromError(ExpressionsResult.Error);
        }

        if (Index >= Source.Length || Source[Index] is not '}') {
            return Result<BoxExpression, string>.FromError("Expected `}` to end box");
        }
        Index++;

        BoxExpression BoxExpression = new(ExpressionsResult.Value, default);
        return Result<BoxExpression, string>.FromValue(BoxExpression);
    }
    private Result<List<Expression>, string> ParseTuple() {
        if (Source[Index] is not '(') {
            return Result<List<Expression>, string>.FromError("Expected `(` to start tuple");
        }
        Index++;

        List<Expression> Expressions = [];

        while (true) {
            ReadWhitespace();

            if (Index >= Source.Length || Source[Index] is ')') {
                break;
            }

            Result<Expression, string> ExpressionResult = ParseExpression();
            if (ExpressionResult.IsError) {
                return Result<List<Expression>, string>.FromError(ExpressionResult.Error);
            }

            ReadWhitespace();

            if (Index >= Source.Length || Source[Index] is not ',') {
                break;
            }
        }

        if (Index >= Source.Length || Source[Index] is not ')') {
            return Result<List<Expression>, string>.FromError("Expected `)` to end tuple");
        }
        Index++;

        return Result<List<Expression>, string>.FromValue(Expressions);
    }
    private void ReadWhitespace() {
        for (; Index < Source.Length; Index++) {
            if (!WhitespaceChars.Contains(Source[Index])) {
                return;
            }
        }
    }
    private Result<Success, string> ReadString() {
        if (Index >= Source.Length || Source[Index] is not ('"' or '\'')) {
            return Result<Success, string>.FromError("Expected string, got nothing");
        }
        char StartChar = Source[Index];
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] == StartChar) {
                Index++;
                return Result<Success, string>.FromSuccess();
            }
        }

        return Result<Success, string>.FromError("Expected end of string, got end of input");
    }
    private Result<Success, string> ReadNumber() {
        if (Index <= Source.Length && Source[Index] is '-' or '+') {
            Index++;
        }

        if (Source[Index] is not (>= '0' and <= '9')) {
            return Result<Success, string>.FromError("Expected digit to start number");
        }
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] is (>= '0' and <= '9')) {
                continue;
            }
            if (Source[Index] is '.') {
                if (Source[Index - 1] is not (>= '0' and <= '9')) {
                    return Result<Success, string>.FromError("Expected digit before `.` in number");
                }
                continue;
            }
            if (Source[Index] is '_') {
                if (Source[Index - 1] is not (>= '0' and <= '9')) {
                    return Result<Success, string>.FromError("Expected digit before `_` in number");
                }
                continue;
            }
            break;
        }

        if (Source[Index - 1] is '_') {
            return Result<Success, string>.FromError("Trailing `_` in number");
        }

        return Result<Success, string>.FromSuccess();
    }
    private Result<Success, string> ReadIdentifier() {
        if (Source[Index] is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_')) {
            return Result<Success, string>.FromError("Expected letter or `_` to start identifier");
        }
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') {
                continue;
            }
            return Result<Success, string>.FromSuccess();
        }

        return Result<Success, string>.FromSuccess();
    }
}

public abstract class Expression {
}
public class MultiExpression : Expression {
    public List<Expression> Expressions { get; set; }

    public MultiExpression(List<Expression> Expressions) {
        this.Expressions = Expressions;
    }
}
public class GetExpression : Expression {
    public Expression? Target { get; set; }
    public string Member { get; set; }

    public GetExpression(Expression? Target, string Member) {
        this.Target = Target;
        this.Member = Member;
    }
}
public class AssignExpression : Expression {
    public Expression? Target { get; set; }
    public string Member { get; set; }
    public Expression Value { get; set; }

    public AssignExpression(Expression? Target, string Member, Expression Value) {
        this.Target = Target;
        this.Member = Member;
        this.Value = Value;
    }
}
public class CallExpression : Expression {
    public Expression Target { get; set; }
    public Expression Argument { get; set; }

    public CallExpression(Expression Target, Expression Argument) {
        this.Target = Target;
        this.Argument = Argument;
    }
}
public class BoxExpression : Expression {
    public Expression? Expression { get; set; }
    public BoxMethod Method { get; set; }

    public BoxExpression(Expression? Expression, BoxMethod Method) {
        this.Expression = Expression;
        this.Method = Method;
    }
}
public class StringExpression : Expression {
    public byte[] String { get; set; }

    public StringExpression(byte[] String) {
        this.String = String;
    }
}
public class IntegerExpression : Expression {
    public long Integer { get; set; }

    public IntegerExpression(long Integer) {
        this.Integer = Integer;
    }
}
public class RealExpression : Expression {
    public double Real { get; set; }

    public RealExpression(double Real) {
        this.Real = Real;
    }
}
public class ExternalCallExpression : Expression {
    public Func<Box[], Box> ExternalFunction { get; set; }

    public ExternalCallExpression(Func<Box[], Box> ExternalFunction) {
        this.ExternalFunction = ExternalFunction;
    }
}

public sealed class Box {
    public const string ComponentsVariableName = "components";
    public const string CallVariableName = "call";
    public const string GetVariableName = "get";

    public Actor Actor { get; }
    public Dictionary<string, Box> Variables { get; }
    public BoxMethod Method { get; }
    public object? Data { get; }

    internal Box(Actor Actor, Dictionary<string, Box> Variables, BoxMethod Method, object? Data) {
        this.Actor = Actor;
        this.Variables = Variables;
        this.Method = Method;
        this.Data = Data;
    }
    internal Box(Actor Actor)
        : this(Actor, [], default, null) {
    }
    internal Box(Actor Actor, Box Components, BoxMethod Method, object? Data)
        : this(Actor, new Dictionary<string, Box>() { [ComponentsVariableName] = Components }, Method, Data) {
    }

    public Box? GetVariable(string Name) {
        if (Variables.TryGetValue(Name, out Box? Value)) {
            return Value;
        }
        return null;
    }
    public void SetVariable(string Name, Box? Value) {
        if (Value is null) {
            Variables.Remove(Name);
        }
        else {
            Variables[Name] = Value;
        }
    }
    public IEnumerable<Box>? GetComponents() {
        return GetVariable(ComponentsVariableName)?.Data as IEnumerable<Box>;
    }
    public void SetComponents(IEnumerable<Box> Components) {
        SetVariable(ComponentsVariableName, Actor.CreateList(Components));
    }
    public BoxMethod GetMethod() {
        Box CurrentBox = this;
        while (true) {
            Box NewBox = CurrentBox.GetVariable(CallVariableName) ?? Actor.NullInstance;
            if (NewBox.IsNull()) {
                return CurrentBox.Method;
            }
            CurrentBox = NewBox;
        }
    }
    public bool Includes(Box Box) {
        if (this == Box) {
            return true;
        }

        Queue<Box> Queue = new();
        Queue.Enqueue(Box);
        while (Queue.TryDequeue(out Box? Current)) {
            if (Current == Box) {
                return true;
            }

            IEnumerable<Box>? CurrentComponents = Box.GetComponents();
            if (CurrentComponents is not null) {
                foreach (Box Component in CurrentComponents) {
                    Queue.Enqueue(Component);
                }
            }
        }

        return false;
    }
    public bool IsNull() {
        return Includes(Actor.Null);
    }
}

public struct BoxMethod {
    public List<string>? Parameters { get; set; }
    public Expression? Expression { get; set; }

    public BoxMethod(List<string> Parameters, Expression? Expression) {
        this.Parameters = Parameters;
        this.Expression = Expression;
    }
}

public sealed class Actor {
    private readonly Lock Lock = new();

    public Box Null { get; }
    public Box Boolean { get; }
    public Box Integer { get; }
    public Box Real { get; }
    public Box String { get; }
    public Box List { get; }
    public Box Dictionary { get; }

    public Box NullInstance { get; }
    public Box TrueInstance { get; }
    public Box FalseInstance { get; }

    public Actor() {
        Null = CreateBox();
        Boolean = CreateBox();
        Integer = CreateBox();
        Real = CreateBox();
        String = CreateBox();
        List = CreateBox();
        Dictionary = CreateBox();
        NullInstance = CreateNull();
        TrueInstance = CreateBoolean(true);
        FalseInstance = CreateBoolean(false);
    }
    public Box CreateBox() {
        return new Box(this);
    }
    public Box CreateBox(IEnumerable<Box> Components) {
        return new Box(this, CreateList(Components), default, null);
    }
    public Box CreateBox(BoxMethod Method) {
        return new Box(this, CreateList(), Method, null);
    }
    public Box CreateBox(IEnumerable<Box> Components, BoxMethod Method) {
        return new Box(this, CreateList(Components), Method, null);
    }
    public Box CreateNull() {
        return new Box(this, CreateList(Null), default, null);
    }
    public Box CreateBoolean(bool BooleanData) {
        return new Box(this, CreateList(Boolean), default, BooleanData);
    }
    public Box CreateInteger(long IntegerData) {
        return new Box(this, CreateList(Integer), default, IntegerData);
    }
    public Box CreateReal(double RealData) {
        return new Box(this, CreateList(Real), default, RealData);
    }
    public Box CreateString(byte[] StringData) {
        return new Box(this, CreateList(String), default, StringData);
    }
    public Box CreateString(string StringData) {
        return CreateString(Encoding.UTF8.GetBytes(StringData));
    }
    public Box CreateList(params IEnumerable<Box> ListData) {
        return new Box(this, List, default, ListData);
    }
    public Box CreateDictionary(IReadOnlyDictionary<Box, Box> DictionaryData) {
        return new Box(this, CreateList(Dictionary), default, DictionaryData);
    }
    public Result<Box, string> Evaluate(Box Target, Expression Expression) {
        Stack<Box> Values = new();
        Values.Push(NullInstance);

        Stack<Frame> Frames = new();
        Frames.Push(new Frame(Target, Expression, 1));

        lock (Lock) {
            while (Frames.TryPeek(out Frame? CurrentFrame)) {
                Result<Success, string> EvaluateFrameResult = EvaluateFrame(Values, Frames, CurrentFrame);
                if (EvaluateFrameResult.IsError) {
                    return Result<Box, string>.FromError(EvaluateFrameResult.Error);
                }
            }

            return Result<Box, string>.FromValue(Values.Pop());
        }
    }

    private Result<Success, string> EvaluateFrame(Stack<Box> Values, Stack<Frame> Frames, Frame CurrentFrame) {
        switch (CurrentFrame.Expression) {

            case MultiExpression MultiExpression:
                if (CurrentFrame.Counter < MultiExpression.Expressions.Count) {
                    while (Values.Count > CurrentFrame.ValuesCount) {
                        Values.Pop();
                    }
                    Expression NextExpression = MultiExpression.Expressions[CurrentFrame.Counter];
                    Frames.Push(new Frame(CurrentFrame.Target, NextExpression, Values.Count));
                }
                else {
                    Frames.Pop();
                    break;
                }
                break;

            case GetExpression GetExpression:
                if (GetExpression.Target is not null) {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Frames.Push(new Frame(CurrentFrame.Target, GetExpression.Target, Values.Count));
                            break;
                        case 1:
                            Box GetTarget = Values.Pop();

                            Box? GetValue = GetTarget.GetVariable(GetExpression.Member);
                            if (GetValue is null) {
                                return Result<Success, string>.FromError($"Variable `{GetExpression.Member}` not found");
                            }
                            Values.Push(GetValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                else {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Box? GetValue = CurrentFrame.Target.GetVariable(GetExpression.Member);
                            if (GetValue is null) {
                                return Result<Success, string>.FromError($"Variable `{GetExpression.Member}` not found");
                            }
                            Values.Push(GetValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                break;

            case AssignExpression AssignExpression:
                if (AssignExpression.Target is not null) {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Frames.Push(new Frame(CurrentFrame.Target, AssignExpression.Target, Values.Count));
                            break;
                        case 1:
                            Frames.Push(new Frame(CurrentFrame.Target, AssignExpression.Value, Values.Count));
                            break;
                        case 2:
                            Box AssignValue = Values.Pop();
                            Box AssignTarget = Values.Pop();

                            AssignTarget.SetVariable(AssignExpression.Member, AssignValue);
                            Values.Push(AssignValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                else {
                    switch (CurrentFrame.Counter) {
                        case 0:
                            Frames.Push(new Frame(CurrentFrame.Target, AssignExpression.Value, Values.Count));
                            break;
                        case 1:
                            Box AssignValue = Values.Pop();

                            CurrentFrame.Target.SetVariable(AssignExpression.Member, AssignValue);
                            Values.Push(AssignValue);
                            break;
                        default:
                            Frames.Pop();
                            break;
                    }
                }
                break;

            case CallExpression CallExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Frames.Push(new Frame(CurrentFrame.Target, CallExpression.Target, Values.Count));
                        break;
                    case 1:
                        Frames.Push(new Frame(CurrentFrame.Target, CallExpression.Argument, Values.Count));
                        break;
                    case 2:
                        Box CallArgument = Values.Pop();
                        Box CallTarget = Values.Pop();

                        BoxMethod CallMethod = CallTarget.GetMethod();

                        Box CallScope = new(this, CreateList(CallTarget), CallMethod, null);
                        Values.Push(CallScope);
                        if (CallScope.Method.Parameters is not null) {
                            for (int Index = CallScope.Method.Parameters.Count - 1; Index >= 0; Index--) {
                                Box? ArgumentGetter = CallArgument.GetVariable(Box.GetVariableName);
                                if (ArgumentGetter is null) {
                                    return Result<Success, string>.FromError($"Missing method `{Box.GetVariableName}`");
                                }

                                Result<Success, string> EvaluateCallResult = EvaluateCall(ArgumentGetter, [CreateInteger(Index)], Values, Frames, CurrentFrame);
                                if (EvaluateCallResult.IsError) {
                                    return EvaluateCallResult;
                                }
                            }
                        }
                        break;
                    case 3:
                        Box[] Arguments = new Box[Values.Count - CurrentFrame.ValuesCount - 1];
                        for (int Index = 0; Index < Arguments.Length; Index++) {
                            Arguments[Index] = Values.Pop();
                        }
                        Box CallScope2 = Values.Pop();

                        Result<Success, string> EvaluateCallResult2 = EvaluateCall(CallScope2, Arguments, Values, Frames, CurrentFrame);
                        if (EvaluateCallResult2.IsError) {
                            return EvaluateCallResult2;
                        }
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case BoxExpression BoxExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Box Box = CreateBox(BoxExpression.Method);
                        Values.Push(Box);
                        if (BoxExpression.Expression is not null) {
                            Frames.Push(new Frame(Box, BoxExpression.Expression, Values.Count));
                        }
                        break;
                    case 1:
                        while (Values.Count > CurrentFrame.ValuesCount + 1) {
                            Values.Pop();
                        }
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case StringExpression StringExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Values.Push(CreateString(StringExpression.String));
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case IntegerExpression IntegerExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Values.Push(CreateInteger(IntegerExpression.Integer));
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case RealExpression RealExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        Values.Push(CreateReal(RealExpression.Real));
                        break;
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            case ExternalCallExpression ExternalCallExpression:
                switch (CurrentFrame.Counter) {
                    case 0:
                        throw new NotImplementedException();
                    default:
                        Frames.Pop();
                        break;
                }
                break;

            default:
                return Result<Success, string>.FromError($"Unknown expression: {CurrentFrame.Expression.GetType()}");

        }

        CurrentFrame.Counter++;

        return Result<Success, string>.FromSuccess();
    }
    private Result<Success, string> EvaluateCall(Box Box, scoped ReadOnlySpan<Box> Arguments, Stack<Box> Values, Stack<Frame> Frames, Frame CurrentFrame) {
        Box CallScope = new(this, CreateList(Box), Box.GetMethod(), null);
        Values.Push(CallScope);

        if (Arguments.Length > (CallScope.Method.Parameters?.Count ?? 0)) {
            return Result<Success, string>.FromError($"Too many arguments (expected {CallScope.Method.Parameters?.Count ?? 0}, got {Arguments.Length})");
        }

        if (CallScope.Method.Parameters is not null) {
            for (int ParameterIndex = 0; ParameterIndex < CallScope.Method.Parameters.Count; ParameterIndex++) {
                string Parameter = CallScope.Method.Parameters[ParameterIndex];

                if (ParameterIndex >= Arguments.Length) {
                    return Result<Success, string>.FromError($"Missing argument for parameter: `{Parameter}`");
                }

                CallScope.SetVariable(Parameter, Arguments[ParameterIndex]);
            }
        }

        if (CallScope.Method.Expression is null) {
            Values.Push(NullInstance);
        }
        else {
            Frames.Push(new Frame(CallScope, CallScope.Method.Expression, Values.Count));
        }

        return Result<Success, string>.FromSuccess();
    }


    private sealed class Frame {
        public Box Target { get; set; }
        public Expression Expression { get; set; }
        public int Counter { get; set; }
        public int ValuesCount { get; set; }

        public Frame(Box Target, Expression Expression, int ValuesCount) {
            this.Target = Target;
            this.Expression = Expression;
            Counter = 0;
            this.ValuesCount = ValuesCount;
        }
    }
}