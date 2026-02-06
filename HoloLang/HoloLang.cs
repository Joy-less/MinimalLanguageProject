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

    public static Result<List<Expression>, string> Parse(string Source) {
        Parser Parser = new(Source);

        Result<List<Expression>, string> ExpressionsResult = Parser.ParseExpressions();
        if (ExpressionsResult.IsError) {
            return Result<List<Expression>, string>.FromError(ExpressionsResult.Error);
        }

        Result<Success, string> EndOfInputResult = Parser.ParseEndOfInput();
        if (EndOfInputResult.IsError) {
            return Result<List<Expression>, string>.FromError(EndOfInputResult.Error);
        }

        return Result<List<Expression>, string>.FromValue(ExpressionsResult.Value);
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
    private Result<List<Expression>, string> ParseExpressions() {
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
                    return Result<List<Expression>, string>.FromError(StringResult.Error);
                }
                ReadOnlySpan<char> String = Source.AsSpan(StringStartIndex..Index);

                // Create literal string expression
                Variant Variant = Variant.FromString(new string(String));
                Expressions.Add(new VariantExpression(Variant));
            }
            // Number
            else if (Source[Index] is (>= '0' and <= '9') or '-' or '+') {
                // Consume number
                int NumberStartIndex = Index;
                Result<Success, string> NumberResult = ReadNumber();
                if (NumberResult.IsError) {
                    return Result<List<Expression>, string>.FromError(NumberResult.Error);
                }
                ReadOnlySpan<char> Number = Source.AsSpan(NumberStartIndex..Index);

                // Create literal number expression
                Variant Variant = Number.Contains('.')
                    ? Variant.FromReal(double.Parse(Number))
                    : Variant.FromInteger(long.Parse(Number));
                Expressions.Add(new VariantExpression(Variant));
            }
            // Identifier
            else if (Source[Index] is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_') {
                // Consume identifier
                int IdentifierStartIndex = Index;
                Result<Success, string> IdentifierResult = ReadIdentifier();
                if (IdentifierResult.IsError) {
                    return Result<List<Expression>, string>.FromError(IdentifierResult.Error);
                }
                ReadOnlySpan<char> Identifier = Source.AsSpan(IdentifierStartIndex..Index);

                // Create get expression
                Expressions.Add(new GetExpression(null, new string(Identifier)));
            }
            // Variant
            else if (Source[Index] is '{') {
                // Consume variant
                Result<Variant, string> VariantResult = ParseVariant();
                if (VariantResult.IsError) {
                    return Result<List<Expression>, string>.FromError(VariantResult.Error);
                }

                // Create variant expression
                Expressions.Add(new VariantExpression(VariantResult.Value));
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

        return Result<List<Expression>, string>.FromValue(Expressions);
    }
    private Result<Variant, string> ParseVariant() {
        if (Source[Index] is not '{') {
            return Result<Variant, string>.FromError("Expected `{` to start variant");
        }
        Index++;

        Result<List<Expression>, string> ExpressionsResult = ParseExpressions();
        if (ExpressionsResult.IsError) {
            return Result<Variant, string>.FromError(ExpressionsResult.Error);
        }

        if (Source[Index] is not '}') {
            return Result<Variant, string>.FromError("Expected `}` to end variant");
        }
        Index++;

        Variant Variant = Variant.From(Variant.FromList(), ExpressionsResult.Value, null);
        return Result<Variant, string>.FromValue(Variant);
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
public class GetExpression : Expression {
    public Expression? Chain { get; }
    public string Member { get; set; }

    public GetExpression(Expression? Chain, string Member) {
        this.Chain = Chain;
        this.Member = Member;
    }
}
public class CallExpression : Expression {
}
public class AssignExpression : Expression {
}
public class VariantExpression : Expression {
    public Variant Variant { get; }

    public VariantExpression(Variant Variant) {
        this.Variant = Variant;
    }
}
public class ExternalCallExpression : Expression {
    public Func<Variant[], Variant> ExternalFunction { get; }

    public ExternalCallExpression(Func<Variant[], Variant> ExternalFunction) {
        this.ExternalFunction = ExternalFunction;
    }
}

public sealed class Variant {
    public const string ComponentsVariable = "components";
    public const string CallVariable = "call";

    public Dictionary<string, Variant> Variables { get; }
    public List<Expression> Expressions { get; }
    public object? Data { get; }

    public static Variant Null { get; } = new();
    public static Variant Boolean { get; } = new();
    public static Variant Integer { get; } = new();
    public static Variant Real { get; } = new();
    public static Variant String { get; } = new();
    public static Variant List { get; } = new();
    public static Variant Dictionary { get; } = new();

    private Variant() {
        Variables = [];
        Expressions = [];
        Data = null;
    }
    private Variant(Dictionary<string, Variant> Variables, List<Expression> Expressions, object? Data) {
        this.Variables = Variables;
        this.Expressions = Expressions;
        this.Data = Data;
    }

    public static Variant From(Variant Components, List<Expression> Expressions, object? Data) {
        return new Variant(new Dictionary<string, Variant>() { [ComponentsVariable] = Components }, Expressions, Data);
    }
    public static Variant FromBoolean(bool BooleanData) {
        return From(FromList(Boolean), [], BooleanData);
    }
    public static Variant FromInteger(long IntegerData) {
        return From(FromList(Integer), [], IntegerData);
    }
    public static Variant FromReal(double RealData) {
        return From(FromList(Real), [], RealData);
    }
    public static Variant FromString(byte[] StringData) {
        return From(FromList(String), [], StringData);
    }
    public static Variant FromString(string StringData) {
        return FromString(Encoding.UTF8.GetBytes(StringData));
    }
    public static Variant FromList(params IEnumerable<Variant> ListData) {
        return From(List, [], ListData);
    }
    public static Variant FromDictionary(IReadOnlyDictionary<Variant, Variant> DictionaryData) {
        return From(FromList(Dictionary), [], DictionaryData);
    }

    public Variant GetVariable(string Name) {
        if (Variables.TryGetValue(Name, out Variant? Value)) {
            return Value;
        }
        return Null;
    }
    public void SetVariable(string Name, Variant? Value) {
        if (Value is null || Value == Null) {
            Variables.Remove(Name);
        }
        else {
            Variables[Name] = Value;
        }
    }
    public IEnumerable<Variant>? GetComponents() {
        return GetVariable(ComponentsVariable).Data as IEnumerable<Variant>;
    }
    public void SetComponents(IEnumerable<Variant> Components) {
        SetVariable(ComponentsVariable, FromList(Components));
    }
}