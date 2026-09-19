using System.Globalization;

namespace SuperCalc.Core;

/// <summary>A bounded decimal expression parser. No eval, network or dynamic code.</summary>
public static class Calculator
{
    public static decimal Evaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new FormatException("先输入一个算式吧。");
        if (expression.Length > 512) throw new FormatException("算式太长了，请控制在 512 个字符以内。");
        return new Parser(expression.Replace('×', '*').Replace('÷', '/').Replace('−', '-')).Parse();
    }

    // Keep tiny values in fixed notation so memory recall and chained operations can parse them again.
    public static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private sealed class Parser(string text)
    {
        private int position;
        private int depth;
        public decimal Parse()
        {
            var result = Sum();
            Space();
            if (position != text.Length) throw new FormatException($"第 {position + 1} 个字符无法识别。");
            return result;
        }

        private decimal Sum()
        {
            var value = Product();
            while (true)
            {
                if (Take('+')) value = checked(value + Product());
                else if (Take('-')) value = checked(value - Product());
                else return value;
            }
        }

        private decimal Product()
        {
            var value = Unary();
            while (true)
            {
                if (Take('*')) value = checked(value * Unary());
                else if (Take('/'))
                {
                    var divisor = Unary();
                    if (divisor == 0) throw new DivideByZeroException("零不能做除数，升级会员也不行。");
                    value /= divisor;
                }
                else return value;
            }
        }

        private decimal Unary()
        {
            if (++depth > 64) throw new FormatException("嵌套太深了，最多支持 64 层。");
            try
            {
                if (Take('+')) return Unary();
                if (Take('-')) return -Unary();
                return Power();
            }
            finally { depth--; }
        }

        private decimal Power()
        {
            var value = Primary();
            while (Take('%')) value /= 100m;
            if (Take('^'))
            {
                var exponent = Unary();
                if (exponent != decimal.Truncate(exponent) || Math.Abs(exponent) > 100)
                    throw new FormatException("幂运算支持 -100 到 100 之间的整数指数。");
                if (value == 0 && exponent < 0) throw new DivideByZeroException("零不能做除数。");
                decimal result = 1;
                // Invert first so large bases with negative exponents don't overflow unnecessarily.
                var factor = exponent < 0 ? 1 / value : value;
                for (var i = 0; i < Math.Abs(exponent); i++) result = checked(result * factor);
                value = result;
            }
            return value;
        }

        private decimal Primary()
        {
            Space();
            if (Take('('))
            {
                var value = Sum();
                if (!Take(')')) throw new FormatException("缺少一个右括号 )。");
                return value;
            }
            if (text.AsSpan(position).StartsWith("sqrt", StringComparison.OrdinalIgnoreCase))
            {
                position += 4;
                if (!Take('(')) throw new FormatException("平方根的写法是 sqrt(数字)。");
                var value = Sum();
                if (!Take(')')) throw new FormatException("平方根缺少右括号。");
                if (value < 0) throw new FormatException("实数模式下，负数没有平方根。");
                return (decimal)Math.Sqrt((double)value);
            }
            Space();
            var start = position;
            while (position < text.Length && (char.IsAsciiDigit(text[position]) || text[position] == '.')) position++;
            if (start == position || !decimal.TryParse(text[start..position], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
                throw new FormatException("这里需要一个有效数字，例如 3.14。");
            return number;
        }

        private bool Take(char token)
        {
            Space();
            if (position >= text.Length || text[position] != token) return false;
            position++;
            return true;
        }
        private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
    }
}
