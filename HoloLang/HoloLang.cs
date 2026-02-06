using System.Buffers.Binary;
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

    public static Result<Success, E> Success() {
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
    public List<Expression> Expressions { get; private set; }

    private static readonly char[] NewlineChars = ['\n', '\r'];
    private static readonly char[] WhitespaceChars = [' ', '\t', '\v', '\f', ..NewlineChars];

    private Parser(string Source) {
        this.Source = Source;
        Index = 0;
        Expressions = [];
    }

    public static List<Expression> Parse(string Source) {
        Parser Parser = new(Source);

        Parser.ConsumeExpressions();

        return Parser.Expressions;
    }

    private Result<Success, string> ConsumeExpressions() {
        for (; Index < Source.Length; Index++) {
            ConsumeWhitespace();

            if (Source[Index] is '"' or '\'') {
                Result<string, string> StringResult = ConsumeString();
                if (StringResult.IsError) {
                    return Result<Success, string>.FromError(StringResult.ErrorOrDefault);
                }
                Variant Variant = Variant.FromString(StringResult.Value);
                Expressions.Add(new VariantExpression(Variant));
            }
            else if (Source[Index] is (>= '0' and <= '9') or '-' or '+') {
                Result<string, string> NumberResult = ConsumeNumber();
                if (NumberResult.IsError) {
                    return Result<Success, string>.FromError(NumberResult.ErrorOrDefault);
                }
                Variant Variant = NumberResult.Value.Contains('.')
                    ? Variant.FromReal(double.Parse(NumberResult.Value))
                    : Variant.FromInteger(long.Parse(NumberResult.Value));
                Expressions.Add(new VariantExpression(Variant));
            }

            ConsumeWhitespace();

            if (Index < Source.Length) {
                if (Source[Index] is not ';') {
                    return Result<Success, string>.FromError("Expected semicolon before next expression");
                }
            }
        }
        return Result<Success, string>.Success();
    }
    private void ConsumeWhitespace() {
        for (; Index < Source.Length; Index++) {
            if (!WhitespaceChars.Contains(Source[Index])) {
                return;
            }
        }
    }
    private Result<string, string> ConsumeString() {
        int StartIndex = Index;
        if (Index >= Source.Length || Source[Index] is not ('"' or '\'')) {
            return Result<string, string>.FromError("Expected string, got nothing");
        }
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] == Source[StartIndex]) {
                Index++;
                return Result<string, string>.FromValue(Source[StartIndex..Index]);
            }
        }

        return Result<string, string>.FromError("Expected end of string, got end of input");
    }
    private Result<string, string> ConsumeNumber() {
        int StartIndex = Index;
        if (Index <= Source.Length && Source[Index] is '-' or '+') {
            Index++;
        }

        if (Source[Index] is not (>= '0' and <= '9')) {
            return Result<string, string>.FromError("Expected digit to start number");
        }
        Index++;

        for (; Index < Source.Length; Index++) {
            if (Source[Index] is (>= '0' and <= '9')) {
                continue;
            }
            if (Source[Index] is '.') {
                if (Source[Index - 1] is not (>= '0' and <= '9')) {
                    return Result<string, string>.FromError("Expected digit before dot in number");
                }
                continue;
            }
            if (Source[Index] is '_') {
                if (Source[Index - 1] is not (>= '0' and <= '9')) {
                    return Result<string, string>.FromError("Expected digit before underscore in number");
                }
                continue;
            }
            break;
        }

        if (Source[Index - 1] is '_') {
            return Result<string, string>.FromError("Trailing underscore in number");
        }

        return Result<string, string>.FromValue(Source[StartIndex..Index]);
    }
}

public abstract class Expression {
}
public class GetExpression : Expression {
    public Expression? Chain { get; }
    public Expression Member { get; set; }

    public GetExpression(Expression? Chain, Expression Member) {
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

public sealed class Variant {
    public const string ComponentsVariable = "components";
    public const string CallVariable = "call";

    private readonly Dictionary<string, Variant> Variables;
    //private readonly List<Expression> Code;
    private readonly object? Data;

    public static Variant Null { get; } = new();
    public static Variant Boolean { get; } = new();
    public static Variant Integer { get; } = new();
    public static Variant Real { get; } = new();
    public static Variant String { get; } = new();
    public static Variant List { get; } = new();
    public static Variant Dictionary { get; } = new();

    public Variant() {
        Variables = [];
        Data = null;
    }

    private Variant(Dictionary<string, Variant> Variables, object? Data) {
        this.Variables = Variables;
        this.Data = Data;
    }

    public static Variant From(Variant Components, object? Data) {
        return new Variant(new Dictionary<string, Variant>() { [ComponentsVariable] = Components }, Data);
    }
    public static Variant From(IEnumerable<Variant> Components, object? Data) {
        return From(FromList(Components), Data);
    }
    public static Variant FromBoolean(bool BooleanData) {
        return From([Boolean], BooleanData);
    }
    public static Variant FromInteger(long IntegerData) {
        return From([Integer], IntegerData);
    }
    public static Variant FromReal(double RealData) {
        return From([Real], RealData);
    }
    public static Variant FromString(byte[] StringData) {
        return From([String], StringData);
    }
    public static Variant FromString(string StringData) {
        return FromString(Encoding.UTF8.GetBytes(StringData));
    }
    public static Variant FromList(IEnumerable<Variant> ListData) {
        return From(List, ListData);
    }
    public static Variant FromDictionary(IReadOnlyDictionary<Variant, Variant> DictionaryData) {
        return From([Dictionary], DictionaryData);
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