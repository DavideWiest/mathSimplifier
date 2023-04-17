using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace mathSimplifierImprovedDesign
{

    [Serializable]
    class InvalidTermException : Exception
    {
        public InvalidTermException(string name)
            : base(String.Format("Invalid term: {0}", name))
        {

        }
    }

    [Serializable]
    class InvalidMExpressionException : Exception
    {
        public InvalidMExpressionException(string name)
            : base(String.Format("Invalid term: {0}", name))
        {

        }
    }

    public enum ops
    {
        PLUS, MINUS, MUL, DIV, EXP, NONE
    }

    public class Program
    {
        static void Main(string[] args)
        {
            string termStr = "2YX+2Y/(-3X/4)*-352/5*34+44";
            //Console.Write("Expression to simplify: ");
            //string term = Console.ReadLine();

            printHandler.DebugMode = true;
            printHandler.InfoMode = true;

            string preparedString = mathParser.PrepareStrToBeParsed(termStr);
            MultiExpression term = mathParser.ParseMultiExpression(termStr);
            MultiExpression simplifiedTerm = mathSimplifier.Simplify(term);

            Console.WriteLine("Simplified Term:");
            Console.WriteLine(simplifiedTerm.ToString());
        }
    }

    public static class mathStrProc
    {
        private static Dictionary<char, ops> opsByChar = new()
        {
            { '+', ops.PLUS },
            { '-', ops.MINUS },
            { '*', ops.MUL },
            { '/', ops.DIV },
            { '^', ops.EXP },
            { '$', ops.NONE }
        };

        public static bool IsOperator(char c)
        {
            return new List<char>(opsByChar.Keys).Contains(c);
        }

        public static char OpToChar(ops op)
        {
            return opsByChar.FirstOrDefault(x => x.Value == op).Key;
        }

        public static ops CharToOp(char c)
        {
            return opsByChar[c];
        }
    }

    public static class mathNumAndOpProc
    {
        public static Dictionary<ops, Func<double, double, double>> reduceByOperator = new()
        {
            { ops.PLUS, new((a, b) => a+b) },
            { ops.MINUS, new((a, b) => a-b) },
            { ops.MUL, new((a, b) => a*b) },
            { ops.DIV, new((a, b) => a/b) },
            { ops.EXP, new(Math.Pow) }
        };

        public static bool IsFirstLevel(ops op)
        {
            return op == ops.PLUS || op == ops.MINUS;
        }

        public static bool IsSecondLevel(ops op)
        {
            return op == ops.MUL || op == ops.DIV;
        }

        public static bool IsThirdLevel(ops op)
        {
            return op == ops.EXP;
        }
    }

    public static class printHandler
    {
        private static int infoIndent = 30;
        private static int debugIndent = 30;

        static bool _PrintInfos = false;
        static bool _debugMode = false;
        public static bool InfoMode { get { return _PrintInfos; } set { _PrintInfos = value; } }
        public static bool DebugMode { get { return _debugMode; } set { _debugMode = value; } }

        public static void PrintInfo(string description, string value)
        {
            if (InfoMode)
                Console.WriteLine(padStr(description, infoIndent) + value);
        }

        public static void PrintDebugInfo(string description, string value)
        {
            if (DebugMode)
                Console.WriteLine(padStr(description, debugIndent) + value);
        }

        public static string padStr(string str, int len)
        {
            return str.PadRight(len).Substring(0, len);
        }
    }

    public static class mathParser
    {

        public static string PrepareStrToBeParsed(string str)
        {
            str = removeWhitespaces(str);
            str = addEndingOperatorIfNeeded(str);
            str = insertMultiplicationSigns(str);

            return str;
        }

        private static string removeWhitespaces(string str)
        {
            return str.Replace(" ", "");
        }

        private static string addEndingOperatorIfNeeded(string str)
        {
            if (str[str.Length - 1] != '$')
                str = str + "$";
            return str;
        }

        private static string insertMultiplicationSigns(string str)
        {
            int offset = 0;
            foreach (int i in Enumerable.Range(1, str.Length - 1))
            {
                if (str[offset + i] == '(')
                {
                    if (!mathStrProc.IsOperator(str[offset + i - 1]))
                    {
                        str = str.Insert(offset + i, "*");
                        offset++;
                    }
                }
            }
            return str;
        }

        public static MultiExpression ParseMultiExpression(string preparsedStr)
        {
            return ParseMultiExpression(preparsedStr, ops.PLUS);
        }

        public static MultiExpression ParseMultiExpression(string preparsedStr, ops op)
        {
            string str = preparsedStr;
            List<Expression> exprs = new();

            string currentStr = "";
            (str, ops prevOp) = determineFirstOp(str);

            bool prevWasSubTerm = false;
            bool prevOpChanged = false;

            for (int i = 0; i < str.Length; i++)
            {

                char c = str[i];

                if (ifIsMinusForNumericalVal(str, i) && currentStr.Equals(""))
                {
                    currentStr = currentStr + c;
                    continue;
                }

                if (mathStrProc.IsOperator(c))
                {
                    if (MovingToSecondLevelOperation(prevOp, mathStrProc.CharToOp(c)) && prevOpChanged)
                    {
                        (exprs, i) = HandleExtractMultiExpressionThroughSecondLevelOperation(exprs, str, currentStr, i, prevOp);

                        prevOp = mathStrProc.CharToOp(c);
                        prevOpChanged = true;
                        prevWasSubTerm = true;
                        continue;
                    }
                    else if (MovingToThirdLevelOperation(prevOp, mathStrProc.CharToOp(c)) && prevOpChanged)
                    {
                        (exprs, i) = HandleExtractMultiExpressionThroughThirdLevelOperation(exprs, str, currentStr, i, prevOp);

                        prevOp = mathStrProc.CharToOp(c);
                        prevOpChanged = true;
                        prevWasSubTerm = true;
                        continue;
                    }
                    else if (prevWasSubTerm == false)
                    {
                        exprs = HandleExtractSingleExpression(exprs, currentStr, prevOp);

                        prevOpChanged = true;
                    }

                    prevOp = mathStrProc.CharToOp(c);
                    currentStr = "";
                    prevWasSubTerm = false;
                }
                else if (c == '(')
                {
                    (exprs, i) = HandleExtractMultiExpressionThroughBraces(exprs, str, i, prevOp);

                    currentStr = "";
                    prevWasSubTerm = true;
                }
                else
                {
                    currentStr = currentStr + c;
                }
            }


            return new MultiExpression(op, exprs);
        }

        private static List<Expression> HandleExtractSingleExpression(List<Expression> exprs, string currentStr, ops prevOp)
        {
            printHandler.PrintDebugInfo("Expression", $"{currentStr}");

            exprs = AppendSingleExpression(exprs, currentStr, prevOp);

            return exprs;
        }

        private static Tuple<List<Expression>, int> HandleExtractMultiExpressionThroughBraces(List<Expression> exprs, string str, int i, ops prevOp)
        {
            int endOfSubTermIndex = FindRespectiveClosingBracketIndex(str.Substring(i + 1), i);
            int skipToIndex = i + endOfSubTermIndex;
            string mulitExprStr = str.Substring(i + 1, endOfSubTermIndex - 1);

            printHandler.PrintDebugInfo("SubTerm (Braces)", $"{mulitExprStr}");

            ops optionalOperatorToCombineWith = ops.PLUS;
            if (i - 1 >= 0)
                optionalOperatorToCombineWith = str[i - 1] == '-' ? ops.MINUS : ops.PLUS;

            exprs = AppendMultiExpression(exprs, mulitExprStr, prevOp, optionalOperatorToCombineWith);

            return Tuple.Create(exprs, skipToIndex-1);
        }

        private static Tuple<List<Expression>, int> HandleExtractMultiExpressionThroughSecondLevelOperation(List<Expression> exprs, string str, string currentStr, int i, ops prevOp)
        {
            int endOfSubTermIndex = FindEndOfMultiplicativeChain(str.Substring(i + 2)) + 1;
            int skipToIndex = i + endOfSubTermIndex;
            string mulitExprStr = currentStr + str.Substring(i, endOfSubTermIndex + 1);

            printHandler.PrintDebugInfo("SubTerm (SecondLevel)", $"{mulitExprStr}");

            exprs = AppendMultiExpression(exprs, mulitExprStr, prevOp, ops.PLUS);

            return Tuple.Create(exprs, skipToIndex-1);
        }

        private static Tuple<List<Expression>, int> HandleExtractMultiExpressionThroughThirdLevelOperation(List<Expression> exprs, string str, string currentStr, int i, ops prevOp)
        {
            int endOfSubTermIndex = findEndOfExponentialChain(str.Substring(i + 2)) + 1;
            int skipToIndex = i + endOfSubTermIndex;
            string mulitExprStr = currentStr + str.Substring(i, endOfSubTermIndex + 1);

            printHandler.PrintDebugInfo("SubTerm (ThirdLevel)", $"{mulitExprStr}");

            exprs = AppendMultiExpression(exprs, mulitExprStr, prevOp, ops.PLUS);

            return Tuple.Create(exprs, skipToIndex-1);
        }

        private static bool MovingToSecondLevelOperation(ops opBefore, ops opAfter)
        {
            return mathNumAndOpProc.IsFirstLevel(opBefore) && mathNumAndOpProc.IsSecondLevel(opAfter);
        }

        private static bool MovingToThirdLevelOperation(ops opBefore, ops opAfter)
        {
            return (mathNumAndOpProc.IsFirstLevel(opBefore) || mathNumAndOpProc.IsSecondLevel(opBefore)) && mathNumAndOpProc.IsThirdLevel(opAfter);
        }

        private static List<Expression> AppendMultiExpression(List<Expression> exprs, string exprStr, ops op, ops optionalOperatorToCombineWith)
        {
            //if (op == ops.EXP)
            //    throw new InvalidTermException("Operator of MultiExpression-String cannot be exponent.");

            MultiExpression term = ParseMultiExpression(PrepareStrToBeParsed(exprStr), op);
            printHandler.PrintDebugInfo("Multiexpression (325)", term.ToString());
            MultiExpression simplifiedTerm = mathSimplifier.Simplify(term);
            simplifiedTerm = ExprManager.MergeExpressionWithOperator(simplifiedTerm, optionalOperatorToCombineWith);
            exprs.Add(simplifiedTerm);
            return exprs;
        }

        private static List<Expression> AppendSingleExpression(List<Expression> exprs, string exprStr, ops op)
        {
            SingleExpression term = ParseSingleExpression(exprStr, op);
            exprs.Add(term);
            return exprs;
        }

        private static SingleExpression ParseSingleExpression(string exprStr, ops op)
        {
            string mulBase = "";
            double numVal = 1.0;
            bool subPartIsNumeric = false;
            int separationIndex = exprStr.Length + 1;

            while (!subPartIsNumeric)
            {
                separationIndex--;
                subPartIsNumeric = double.TryParse(exprStr.Substring(0, separationIndex), out numVal);

                if (separationIndex == 0)
                    break;
            }

            mulBase = exprStr.Substring(separationIndex);

            SingleExpression expr = new SingleExpression(op, numVal, mulBase);

            validateSingleExpressionExponents(expr);

            return expr;
        }

        private static void validateSingleExpressionExponents(SingleExpression expr)
        {
            if (expr.mulBase != "" && expr.op == ops.EXP)
            {
                if (expr.numVal % 1 != 0 || expr.numVal < 0)
                    throw new InvalidMExpressionException("Only whole numbers above 0 allowed in exponents");
            }
        }

        private static int findEndOfExponentialChain(string substr)
        {

            return firstIndexThatMatchesInFirstBracesLevel(substr, 0, (i) => mathStrProc.IsOperator(substr[i]) && substr[i] != '^' && !ifIsMinusForNumericalVal(substr, i), true);
        }

        private static int FindEndOfMultiplicativeChain(string substr)
        {
            return firstIndexThatMatchesInFirstBracesLevel(substr, 0, (i) => (substr[i] == '-' || substr[i] == '+') && !ifIsMinusForNumericalVal(substr, i), true);
        }

        private static int FindRespectiveClosingBracketIndex(string substr, int beginningFromI)
        {
            return firstIndexThatMatchesInFirstBracesLevel(substr, beginningFromI, (i) => true, false) + 1; // +1: skip closing bracket
        }

        private static int firstIndexThatMatchesInFirstBracesLevel(string substr, int beginningFromI, Func<int, bool> condition, bool returnLastIfNotFound)
        {
            int openBracesToOffset = 1;
            for (int i = 0; i < substr.Length; i++)
            {
                char c = substr[i];

                if (c == '(')
                    openBracesToOffset++;
                else if (c == ')')
                {
                    openBracesToOffset--;
                }

                if (openBracesToOffset == 0)
                {
                    if (condition(i))
                        return i;
                }
            }
            if (returnLastIfNotFound)
                return substr.Length - 1;
            else
                throw new InvalidTermException($"Opening brace at Index {beginningFromI} was never closed");
        }

        private static bool ifIsMinusForNumericalVal(string str, int i)
        {
            if (str[i] != '-')
                return false;
            else
                return isMinusForNumericVal(str, i);
        }

        private static bool isMinusForNumericVal(string str, int i)
        {
            if (str[i] != '-')
                throw new InvalidDataException("The char isnt even a minus");

            if (i > 0)
            {
                if (str[i] == '-' && (str[i - 1] == '*' || str[i - 1] == '/' || str[i - 1] == '('))
                    return true;
            }
            return false;
        }

        private static Tuple<string, ops> determineFirstOp(string str)
        {
            ops prevOp = ops.PLUS;

            if (mathStrProc.IsOperator(str[0]))
            {
                prevOp = mathStrProc.CharToOp(str[0]);
                str = str.Substring(1);
            }
            return Tuple.Create(str, prevOp);
        }
    }

    public static class mathSimplifier
    {

        public static MultiExpression Simplify(MultiExpression input)
        {
            List<Expression> result = new();
            List<Expression> nextExpressionBatch = input.exprs;
            List<Expression> expressionsToSimplify = new();

            do
            {
                expressionsToSimplify.Clear();
                expressionsToSimplify.AddRange(nextExpressionBatch);
                Expression currentSimplifierExpression = expressionsToSimplify[0];

                nextExpressionBatch.Clear();

                for (int i = 1; i < expressionsToSimplify.Count; i++)
                {
                    Expression expr2 = expressionsToSimplify[i];
                    if (combinerChecker.CanCombine(currentSimplifierExpression, expr2))
                    {
                        currentSimplifierExpression = new MultiExpression(currentSimplifierExpression.op, Combiner.Combine(currentSimplifierExpression, expr2));
                        printHandler.PrintInfo($"Simplification step", currentSimplifierExpression.ToString());
                    }
                    else
                    {
                        nextExpressionBatch.Add(expr2);
                    }
                }

                result.Add(currentSimplifierExpression);
            }
            while (nextExpressionBatch.Count != 0);

            return new MultiExpression(input.op, result);
        }
        

        public static MultiExpression SimplifyWithPreferredOperator(MultiExpression input, ops op)
        {
            return input;
        }


    }

    public static class combinerChecker
    {
        public static bool CanCombine(Expression expr1, Expression expr2)
        {
            if (expr1 is SingleExpression)
            {
                if (expr2 is SingleExpression)
                {
                    return SingleWithSingleCombinerChecker.IsCompatible(expr1 as SingleExpression, expr2 as SingleExpression);
                }
                else if (expr2 is MultiExpression)
                {
                    return SingleWithMultiCombinerChecker.IsCompatible(expr1 as SingleExpression, expr2 as MultiExpression);
                }
            }
            else if (expr1 is MultiExpression)
            {
                if (expr2 is SingleExpression)
                {
                    return SingleWithMultiCombinerChecker.IsCompatible(expr1 as MultiExpression, expr2 as SingleExpression);
                }
                else if (expr2 is MultiExpression)
                {
                    return MultiWithMultiCombinerChecker.IsCompatible(expr1 as MultiExpression, expr2 as MultiExpression);
                }
            }

            throw new NotImplementedException("Unknown Expression-extending class");
            
        }
    }

    public static class SingleWithSingleCombinerChecker
    {
        public static bool IsCompatible(SingleExpression sex1, SingleExpression sex2)
        {
            switch (sex2.op)
            {
                case ops.PLUS:
                case ops.MINUS:
                    return FirstLevel(sex1, sex2);
                case ops.MUL:
                case ops.DIV:
                    return SecondLevel(sex1, sex2);
                case ops.EXP:
                    return ThirdLevel(sex1, sex2);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }

        public static bool FirstLevel(SingleExpression sex1, SingleExpression sex2)
        {
            return (sex1.mulBase + sex1.divBase).Equals(sex2.mulBase + sex2.divBase) && SecondLevel(sex1, sex2);
        }

        public static bool SecondLevel(SingleExpression sex1, SingleExpression sex2)
        {
            return (sex1.expMulBase + sex1.expDivBase).Equals(sex2.expMulBase + sex2.expDivBase) && ThirdLevel(sex1, sex2);
        }

        public static bool ThirdLevel(SingleExpression sex1, SingleExpression sex2)
        {
            return true;
        }
    }

    public static class SingleWithMultiCombinerChecker
    {
        public static bool IsCompatible(MultiExpression mex, SingleExpression sex)
        {
            switch (sex.op)
            {
                case ops.PLUS:
                case ops.MINUS:
                    return FirstLevel(sex, mex);
                case ops.MUL:
                case ops.DIV:
                    return SecondLevel(sex, mex);
                case ops.EXP:
                    return ThirdLevel(mex, sex);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }
        public static bool IsCompatible(SingleExpression sex, MultiExpression mex)
        {
            switch (mex.op)
            {
                case ops.PLUS:
                case ops.MINUS:
                    return FirstLevel(sex, mex);
                case ops.MUL:
                case ops.DIV:
                    return SecondLevel(sex, mex);
                case ops.EXP:
                    return ThirdLevel(sex, mex);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }
        public static bool FirstLevel(SingleExpression sex, MultiExpression mex)
        {
            return true;
        }

        public static bool SecondLevel(SingleExpression sex, MultiExpression mex)
        {
            return true;
        }

        public static bool ThirdLevel(MultiExpression mex, SingleExpression sex)
        {
            return true;
        }

        public static bool ThirdLevel(SingleExpression sex, MultiExpression mex)
        {
            return true;
        }
    }

    public static class MultiWithMultiCombinerChecker
    {
        public static bool IsCompatible(MultiExpression mex1, MultiExpression mex2)
        {
            switch (mex2.op)
            {
                case ops.PLUS:
                case ops.MINUS:
                    return FirstLevel(mex1, mex2);
                case ops.MUL:
                case ops.DIV:
                    return SecondLevel(mex1, mex2);
                case ops.EXP:
                    return ThirdLevel(mex1, mex2);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }
        public static bool FirstLevel(MultiExpression mex1, MultiExpression mex2)
        {
            return true;
        }

        public static bool SecondLevel(MultiExpression mex1, MultiExpression mex2)
        {
            return true;
        }

        public static bool ThirdLevel(MultiExpression mex1, MultiExpression mex2)
        {
            return true;
        }
    }

    public static class Combiner
    {
        public static List<Expression> Combine(Expression expr1, Expression expr2)
        {

            if (expr1 is SingleExpression)
            {
                if (expr2 is SingleExpression)
                {
                    return SingleWithSingleCombiner.Combine(expr1 as SingleExpression, expr2 as SingleExpression);
                }
                else if (expr2 is MultiExpression)
                {
                    return SingleWithMultiCombiner.Combine(expr1 as SingleExpression, expr2 as MultiExpression);
                }
            }
            else if (expr1 is MultiExpression)
            {
                if (expr2 is SingleExpression)
                {
                    return SingleWithMultiCombiner.Combine(expr1 as MultiExpression, expr2 as SingleExpression);
                }
                else if (expr2 is MultiExpression)
                {
                    return MultiWithMultiCombiner.Combine(expr1 as MultiExpression, expr2 as MultiExpression);
                }
            }

            throw new NotImplementedException("Unknown Expression-extending class");
        }
    }
    public static class SingleWithSingleCombiner
    {
        public static List<Expression> Combine(SingleExpression sex1, SingleExpression sex2)
        {
            switch (sex2.op)
            {
                case ops.PLUS:
                    return Plus(sex1, sex2);
                case ops.MINUS:
                    return Minus(sex1, sex2);
                case ops.MUL:
                    return Mul(sex1, sex2);
                case ops.DIV:
                    return Div(sex1, sex2);
                case ops.EXP:
                    return Exp(sex1, sex2);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }

        public static List<Expression> Plus(SingleExpression sex1, SingleExpression sex2)
        {
            sex1.numVal += sex2.numVal;
            return new List<Expression> { sex1 };
        }

        public static List<Expression> Minus(SingleExpression sex1, SingleExpression sex2)
        {
            sex1.numVal -= sex2.numVal;
            return new List<Expression> { sex1 };
        }

        public static List<Expression> Mul(SingleExpression sex1, SingleExpression sex2)
        {
            sex1.numVal *= sex2.numVal;
            sex1.mulBase += sex2.mulBase;
            sex1.divBase += sex2.divBase;
            return new List<Expression> { sex1 };
        }

        public static List<Expression> Div(SingleExpression sex1, SingleExpression sex2)
        {
            sex1.numVal -= sex2.numVal;
            sex1.mulBase += sex2.divBase;
            sex1.divBase += sex2.mulBase;
            return new List<Expression> { sex1 };
        }

        public static List<Expression> Exp(SingleExpression sex1, SingleExpression sex2)
        {
            sex1 = moveBasesToExponentAttributes(sex1, sex2);

            if (sex1.expMulBase == "" && sex1.expDivBase == "" && sex2.numVal % 1 == 0 && sex2.numVal >= 0)
            {
                if (sex2.numVal == 0.0)
                {
                    return new List<Expression> { new SingleExpression(sex1.op, 1.0) };
                } else if (sex2.numVal > 1.0) // skipping 1.0 as it doesnt change the expression
                {
                    multiplySingleExpressionNTimes(sex1, Convert.ToInt32(sex2.numVal)-1);
                }
            } else
            {
                sex1.expNumVal += sex2.expNumVal;
            }

            return new List<Expression> { sex1 };
        }

        public static SingleExpression moveBasesToExponentAttributes(SingleExpression sex1, SingleExpression sex2)
        {
            sex1.expMulBase += sex2.expMulBase;
            sex1.expDivBase += sex2.expDivBase;
            sex1.expMulBase += sex2.mulBase;
            sex1.expDivBase += sex2.divBase;
            return sex1;
        }

        public static SingleExpression multiplySingleExpressionNTimes(SingleExpression sex, int n)
        {
            foreach (int i in Enumerable.Range(0, n))
            {
                sex.numVal *= sex.numVal;
                sex.mulBase += sex.mulBase;
                sex.divBase += sex.divBase;
            }
            return sex;
        }
    }
    public static class SingleWithMultiCombiner
    {
        public static List<Expression> Combine(MultiExpression mex, SingleExpression sex)
        {
            return CombineLogic(sex, mex, true);
        }

        public static List<Expression> Combine(SingleExpression sex, MultiExpression mex)
        {
            return CombineLogic(sex, mex, false);
        }
        public static List<Expression> CombineLogic(SingleExpression sex, MultiExpression mex, bool switchInputOrder)
        {
            Expression exprToSwitch = switchInputOrder ? sex : mex;
            switch (exprToSwitch.op)
            {
                case ops.PLUS:
                case ops.MINUS:
                    return PlusMinus(sex, mex, switchInputOrder);
                case ops.MUL:
                case ops.DIV:
                    return MulDiv(sex, mex, switchInputOrder);
                case ops.EXP:
                    return Exp(sex, mex, switchInputOrder);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }

        public static List<Expression> PlusMinus(SingleExpression sex, MultiExpression mex, bool switchInputOrder)
        {
            List<Expression> result = new List<Expression> { sex };
            foreach (Expression expr in mex.exprs)
            {
                result.Add(ExprManager.MergeExpressionWithOperator(expr, mex.op));
            }

            if (!switchInputOrder)
                result.Insert(0, sex);
            else
                result.Add(sex);
            return result;
        }

        public static List<Expression> MulDiv(SingleExpression sex, MultiExpression mex, bool switchInputOrder)
        {
            List<Expression> result = new();
            
            foreach (Expression expr in mex.exprs)
            {
                if (!switchInputOrder)
                    result.AddRange(Combiner.Combine(sex, ExprManager.MergeExpressionWithOperator(expr, mex.op)));
                else
                    result.AddRange(Combiner.Combine(expr, sex));
            }

            return result;
        }

        public static List<Expression> Exp(SingleExpression sex, MultiExpression mex, bool switchInputOrder)
        {
            List<Expression> result = new List<Expression>() { sex };
            
            if (!switchInputOrder)
            {
                foreach (Expression expr in mex.exprs)
                {
                    ops prevOp = expr.op;
                    expr.op = mex.op;

                    if (expr is SingleExpression)
                        result.Add(ExprManager.MergeExpressionWithOperator(Combiner.Combine(sex, expr)[0] as SingleExpression, ExprManager.DetermineHigherLevelOperator(prevOp)));
                    else
                        result.Add(ExprManager.MergeExpressionWithOperator(new MultiExpression(sex.op, Combiner.Combine(sex, expr)), ExprManager.DetermineHigherLevelOperator(prevOp)));
                     // is sex.op correct ?
                }
            }
            else
            {
                result.Add(mex);
                result.Add(sex);
            }

            return result;
        }
    }
    public static class MultiWithMultiCombiner
    {
        public static List<Expression> Combine(MultiExpression mex1, MultiExpression mex2)
        {
            switch (mex2.op)
            {
                case ops.PLUS:
                    return PlusMinusMulDiv(mex1, mex2);
                case ops.MINUS:
                    return PlusMinusMulDiv(mex1, mex2);
                case ops.MUL:
                    return PlusMinusMulDiv(mex1, mex2);
                case ops.DIV:
                    return PlusMinusMulDiv(mex1, mex2);
                case ops.EXP:
                    return Exp(mex1, mex2);
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }

        public static List<Expression> PlusMinusMulDiv(MultiExpression mex1, MultiExpression mex2)
        {
            List<Expression> result = new List<Expression>();

            foreach (Expression expr in mex2.exprs)
            {
                result.AddRange(Combiner.Combine(mex1, ExprManager.MergeExpressionWithOperator(expr, mex2.op)));
            }

            return result;
        }

        public static List<Expression> Exp(MultiExpression mex1, MultiExpression mex2)
        {
            List<Expression> result = new List<Expression>();

            foreach (Expression expr in mex2.exprs)
            {
                foreach (Expression combinedExpr in Combiner.Combine(mex1, ExprManager.MergeExpressionWithOperator(expr, mex2.op))) {
                    combinedExpr.op = ExprManager.DetermineHigherLevelOperator(combinedExpr.op);
                    result.Add(combinedExpr);
                }
            }

            return result;
        }
    }

    public class ExprManager
    {
        public static Expression MergeExpressionWithOperator(Expression expr, ops op)
        {
            if (expr is SingleExpression)
                return MergeExpressionWithOperator(expr as SingleExpression, op);
            else
                return MergeExpressionWithOperator(expr as MultiExpression, op);
        }
        public static SingleExpression MergeExpressionWithOperator(SingleExpression sex, ops op)
        {
            switch (op)
            {
                case ops.PLUS:
                    return sex;
                case ops.MINUS:
                    sex.op = CombineFirstLevelOperators(sex.op, op);
                    return sex;
                case ops.MUL:
                    sex.numVal = CombineNumValWithFirstLevelOperator(sex.numVal, sex.op);
                    sex.op = ops.MUL;
                    return sex;
                case ops.DIV:
                    sex.numVal = CombineNumValWithFirstLevelOperator(sex.numVal, sex.op);
                    sex.op = ops.DIV;
                    return sex;
                case ops.EXP:
                    sex.op = ops.EXP; // TODO
                    return sex;
                default:
                    throw new NotImplementedException("Operator not implemented yet");
            }
        }

        public static MultiExpression MergeExpressionWithOperator(MultiExpression mex, ops op)
        {
            // TODO: 
            switch (op)
            {
                case ops.PLUS:
                    return mex;
                case ops.MINUS:
                    if (mathNumAndOpProc.IsFirstLevel(mex.op))
                        mex.op = CombineFirstLevelOperators(mex.op, op);
                    else
                        mex.exprs = ApplyOperatorCombinationToAll(mex.exprs, op);
                    return mex;
                case ops.MUL:
                case ops.DIV:
                    if (mathNumAndOpProc.IsFirstLevel(mex.op))
                        mex.exprs = ApplyOperatorCombinationToAll(mex.exprs, mex.op);
                    if (!mathNumAndOpProc.IsThirdLevel(mex.op)) // TODO ?
                        mex.op = op;
                    return mex;
                case ops.EXP:
                    mex.op = ops.EXP; // TODO
                    return mex;
                default:
                    throw new NotImplementedException("Operator not implemented yet");
            }
        }

        public static List<Expression> ApplyOperatorCombinationToAll(List<Expression> exprs, ops op)
        {
            if (op == ops.PLUS)
                return exprs;

            exprs.Select(x => MergeExpressionWithOperator(x, op));
            return exprs;
        }

        public static ops CombineFirstLevelOperators(ops op1, ops op2)
        {
            if ((op1 == ops.PLUS) != (op2 == ops.PLUS))
                return ops.MINUS;
            else
                return ops.PLUS;
        }

        public static double CombineNumValWithFirstLevelOperator(double num, ops op)
        {
            if (op == ops.MINUS) 
                return num * -1;
            return num;
        }

        public static ops DetermineHigherLevelOperator(ops op)
        {
            switch (op)
            {
                case ops.PLUS:
                    return ops.MUL;
                case ops.MINUS:
                    return ops.DIV;
                case ops.MUL:
                    return ops.EXP;
                case ops.DIV:
                    throw new NotImplementedException("Divisors not allowed in exponent"); // optional TODO: implement this: implement square root field
                case ops.EXP:
                    return op; // TODO
                default:
                    throw new NotImplementedException("Unknown Operator");
            }
        }
    }

    public class Expression
    {
        public ops op;

        public Expression(ops op)
        {
            this.op = op;
        }
    }

    public class MultiExpression : Expression
    {
        public List<Expression> exprs = new();


        public MultiExpression(ops op, List<Expression> exprs): base(op)
        {
            this.exprs = exprs;
        }

        public override string ToString()
        {
            string str = "";
            foreach (var e in exprs)
            {
                str += e.ToString();
            }

            return $"{mathStrProc.OpToChar(op)}({str})";
        }
    }

    public class SingleExpression : Expression
    {
        public double _numval = 1.0;
        public double _expnumval = 1.0;
        public double numVal
        {
            get => _numval;
            set
            {
                _numval = value;
                CancelOutBaseAndNumMinus();
            }
        }
        public double expNumVal
        {
            get => _expnumval;
            set
            {
                _numval = value;
            }
        }

        public string _mulbase = "";
        public string mulBase
        {
            get => _mulbase;
            set
            {
                _mulbase = String.Concat(value.OrderBy(c => c));
                CancelOutSecondLevelBases();
            }
        }
        public string _divbase = "";
        public string divBase
        {
            get => _divbase;
            set
            {
                _divbase = String.Concat(value.OrderBy(c => c));
                CancelOutSecondLevelBases();
            }
        }
        public string _expmulbase = "";
        public string expMulBase
        {
            get => _expmulbase;
            set
            {
                _expmulbase = String.Concat(value.OrderBy(c => c));
                CancelOutThirdLevelBases();
            }
        }
        public string _expdivbase = "";
        public string expDivBase
        {
            get => _expdivbase;
            set
            {
                _expdivbase = String.Concat(value.OrderBy(c => c));
                CancelOutThirdLevelBases();
            }
        }

        private void CancelOutSecondLevelBases()
        {
            (_mulbase, _divbase) = CancelOutOpposingChars(_mulbase, _divbase);
        }

        private void CancelOutThirdLevelBases()
        {
            (_expmulbase, _expdivbase) = CancelOutOpposingChars(_expmulbase, _expdivbase);
        }

        private Tuple<string, string> CancelOutOpposingChars(string str1, string str2)
        {
            var regex = new Regex(Regex.Escape(""));
            foreach (char c in str1)
            {
                if (str2.Contains(c))
                {
                    str1 = regex.Replace(str1, c.ToString(), 1);
                    str2 = regex.Replace(str2, c.ToString(), 1);
                }
            }
            return Tuple.Create(str1, str2);
        }

        private void CancelOutBaseAndNumMinus()
        {
            (_numval, op) = CancelOutOpposingMinuses(_numval, op);
        }

        private Tuple<double, ops> CancelOutOpposingMinuses(double num, ops op)
        {
            if (num < 0)
            {
                if (op == ops.MINUS)
                {
                    num *= -1;
                    op = ops.PLUS;
                } else if (op == ops.PLUS)
                {
                    num *= -1;
                    op = ops.MINUS;
                }
            }
            return Tuple.Create(num, op);
        }

        public SingleExpression(ops op, double numVal, string mulBase) : base(op)
        {
            this.numVal = numVal;
            this.mulBase = mulBase;
        }

        public SingleExpression(ops op, string mulBase) : base(op)
        {
            this.mulBase = mulBase;
        }

        public SingleExpression(ops op, double numVal) : base(op)
        {
            this.numVal = numVal;
        }

        public SingleExpression(SingleExpression sex) : base(sex.op)
        {
            numVal = sex.numVal;
            mulBase = sex.mulBase;
            divBase = sex.divBase;
            expNumVal = sex.expNumVal;
            expMulBase = sex.expMulBase;
            expDivBase = sex.expDivBase;
        }


        public override string ToString()
        {
            string numValStr = "";
            string divBaseStr = "";

            string expOptionalOpenBrace = "";
            string expBaseStr = "";

            if (numVal != 1.0 || mulBase == "")
                numValStr = $"{numVal}";
            
            if (divBase.Length != 0)
                divBaseStr += $"/{divBase}";
            
            if (expNumVal != 1.0)
                expBaseStr += expNumVal.ToString();
            
            if (expMulBase.Length != 0)
                expBaseStr += expMulBase;

            if (expDivBase.Length != 0)
            {
                if (expBaseStr.Length == 0)
                    expBaseStr += "1";

                expBaseStr += "/";
                expBaseStr += expDivBase;
            }
            if (expBaseStr.Length != 0)
            {
                expOptionalOpenBrace = "(";
                expBaseStr = "(" + expBaseStr + ")";
            }

            return $"{mathStrProc.OpToChar(op)}{expOptionalOpenBrace}{numValStr}{mulBase}{divBaseStr}{expBaseStr}";
        }

    }


















}
