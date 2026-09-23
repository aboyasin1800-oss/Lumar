using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LUMAR_ERP_API_V2.Utilities;

public sealed record ConsumptionFormulaEvaluation(
    bool IsSuccessful,
    decimal? Value,
    IReadOnlyList<string> MissingMeasurements,
    string? ErrorMessage);

public static class ConsumptionFormulaEvaluator
{
    private static readonly Regex TokenRegex = new("[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractMeasurementTokens(string formula)
        => TokenRegex.Matches(formula ?? string.Empty)
            .Select(match => match.Value)
            .Where(token => !token.Equals("plus", StringComparison.OrdinalIgnoreCase))
            .Where(token => !token.Equals("minus", StringComparison.OrdinalIgnoreCase))
            .Where(token => !token.Equals("multiply", StringComparison.OrdinalIgnoreCase))
            .Where(token => !token.Equals("div", StringComparison.OrdinalIgnoreCase))
            .Where(token => !token.Equals("by", StringComparison.OrdinalIgnoreCase))
            .Where(token => !token.Equals("and", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static ConsumptionFormulaEvaluation Evaluate(
        string formula,
        IReadOnlyDictionary<string, decimal> measurements)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return new(false, null, Array.Empty<string>(), "صيغة الاستهلاك فارغة.");
        }

        var values = new Dictionary<string, decimal>(measurements ??
            new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase);
        var missing = ExtractMeasurementTokens(formula)
            .Where(token => !values.ContainsKey(token))
            .ToList();
        if (missing.Count > 0)
        {
            return new(false, null, missing, null);
        }

        try
        {
            var parser = new Parser(formula, values);
            var value = parser.ParseExpression();
            parser.SkipWhitespace();
            if (!parser.IsAtEnd)
            {
                return new(false, null, Array.Empty<string>(), "صيغة الاستهلاك تحتوي على رموز غير مدعومة.");
            }

            return new(true, value, Array.Empty<string>(), null);
        }
        catch (DivideByZeroException)
        {
            return new(false, null, Array.Empty<string>(), "لا يمكن القسمة على صفر في صيغة الاستهلاك.");
        }
        catch (FormatException)
        {
            return new(false, null, Array.Empty<string>(), "صيغة الاستهلاك غير صالحة.");
        }
        catch (OverflowException)
        {
            return new(false, null, Array.Empty<string>(), "نتيجة صيغة الاستهلاك خارج النطاق.");
        }
    }

    private sealed class Parser
    {
        private readonly string _formula;
        private readonly IReadOnlyDictionary<string, decimal> _values;
        private int _position;

        public Parser(string formula, IReadOnlyDictionary<string, decimal> values)
        {
            _formula = formula;
            _values = values;
        }

        public bool IsAtEnd => _position >= _formula.Length;

        public decimal ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('+')) value += ParseTerm();
                else if (TryConsume('-')) value -= ParseTerm();
                else return value;
            }
        }

        private decimal ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (TryConsume('*')) value *= ParseFactor();
                else if (TryConsume('/'))
                {
                    var divisor = ParseFactor();
                    if (divisor == 0) throw new DivideByZeroException();
                    value /= divisor;
                }
                else return value;
            }
        }

        private decimal ParseFactor()
        {
            SkipWhitespace();
            if (TryConsume('+')) return ParseFactor();
            if (TryConsume('-')) return -ParseFactor();
            if (TryConsume('('))
            {
                var value = ParseExpression();
                SkipWhitespace();
                if (!TryConsume(')')) throw new FormatException();
                return value;
            }

            if (!IsAtEnd && (char.IsDigit(_formula[_position]) || _formula[_position] == '.'))
            {
                var start = _position;
                while (!IsAtEnd && (char.IsDigit(_formula[_position]) || _formula[_position] == '.')) _position++;
                var literal = _formula[start.._position];
                if (!decimal.TryParse(literal, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                    throw new FormatException();
                return value;
            }

            if (!IsAtEnd && (char.IsLetter(_formula[_position]) || _formula[_position] == '_'))
            {
                var start = _position++;
                while (!IsAtEnd && (char.IsLetterOrDigit(_formula[_position]) || _formula[_position] == '_')) _position++;
                var token = _formula[start.._position];
                if (!_values.TryGetValue(token, out var value)) throw new FormatException();
                return value;
            }

            throw new FormatException();
        }

        public void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(_formula[_position])) _position++;
        }

        private bool TryConsume(char value)
        {
            if (!IsAtEnd && _formula[_position] == value)
            {
                _position++;
                return true;
            }
            return false;
        }
    }
}
