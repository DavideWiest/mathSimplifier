using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace mathSimplifierDone
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

    public class Program
    {
        public static Dictionary<char, mathOperators> strToOperator = new()
        {
            { '+', mathOperators.PLUS },
            { '-', mathOperators.MINUS },
            { '*', mathOperators.MUL },
            { '/', mathOperators.DIV },
            { '$', mathOperators.NONE }
        };

        public static Dictionary<mathOperators, Func<double, double, double>> opToNumReducerFn = new()
        {
            { mathOperators.PLUS, new((a, b) => a+b) },
            { mathOperators.MINUS, new((a, b) => a-b) },
            { mathOperators.MUL, new((a, b) => a*b) },
            { mathOperators.DIV, new((a, b) => a/b) }
        };

        public static mathOperators mergeLowLevelOperators(mathOperators op1, mathOperators op2)
        {
            // if they arent actually low level (+-)
            if (op1 == mathOperators.MUL || op1 == mathOperators.DIV)
                return op2;
            
            if (op2 == mathOperators.MUL || op2 == mathOperators.DIV)
                return op1;
            

            if ((op1 == mathOperators.PLUS) == (op2 == mathOperators.PLUS))
            {
                return mathOperators.PLUS;
            }
            else
            {
                return mathOperators.MINUS;
            }
        }

        public static string removeAllCharsFromSecondStr(string a, string b)
        {
            foreach (char c in b)
            {
                a = a.Replace(c.ToString(), "");
            }
            return a;
        }

        public static string padStr(string str, int len)
        {
            return str.PadRight(len).Substring(0, len);
        }

        public static Dictionary<mathOperators, string> neutralElementsByOperatorAsStr = new()
        {
            { mathOperators.PLUS, "0" },
            { mathOperators.MINUS, "0" },
            { mathOperators.MUL, "1" },
            { mathOperators.DIV, "1" }
        };

        static void Main2(string[] args)
        {
            string term = "2YX+2Y/(-3X/4)*-352/5*34+44";
            //Console.Write("Expression to simplify: ");
            //string term = Console.ReadLine();
            subTerm newTerm = new subTermImp(term, true);
            newTerm.parse();
            newTerm.simplify();
            Console.WriteLine("Simplified Term:");
            Console.WriteLine(newTerm.ToStringNotNested());
        }
    }

    public enum mathOperators
    {
        PLUS, MINUS, MUL, DIV, NONE
    }

    public interface subTerm
    {
        public void parse();
        public void simplify();
        public string ToString();
        public string ToStringNotNested();
        public List<Expression> getSimplifiedTerms();
        public mathOperators getMOp();
    }

    public class subTermImp : subTerm
    {
        public mathOperators mOp;
        private string startString;
        public ArrayList subTerms = new();
        private List<Expression> simplifiedTerm = new();
        private bool makeInfoPrints = true;
        private int padLen = 20;
        public subTermImp(string startString)
        {
            this.startString = startString;
            mOp = mathOperators.PLUS;
            validateStartStringNotEmpty();
        }
        public subTermImp(string startString, bool makeInfoPrints)
        {
            this.startString = startString;
            mOp = mathOperators.PLUS;
            this.makeInfoPrints = makeInfoPrints;
            validateStartStringNotEmpty();
        }

        public subTermImp(string startString, mathOperators mOp)
        {
            this.startString = startString;
            this.mOp = mOp;
        }
        public subTermImp(List<Expression> simplifiedTerm, mathOperators mOp)
        {
            this.simplifiedTerm = simplifiedTerm;
            this.mOp = mOp;
        }

        private void validateStartStringNotEmpty()
        {
            if (startString.Length == 0)
                throw new InvalidTermException("Input string may nut be empty");
        }
        public void parse()
        {
            satisfyExceptionCases();
            parseToSubTermsAndExpressions();
            applyDistributiveAndAssociativeLaw();
            // all nested subterms must be resolved to expressions by then
        }

        private void satisfyExceptionCases()
        {
            startString = startString.Replace(" ", "");

            if (startString[startString.Length-1] != '$')
                startString = startString + "$";

            insertMultiplicationSigns();
        }

        private void insertMultiplicationSigns()
        {
            int offset = 0;
            foreach (int i in Enumerable.Range(1, startString.Length-1))
            {
                if (startString[offset+i] == '(')
                {
                    if (!isOperator(startString[offset+i - 1]))
                    {
                        startString = startString.Insert(offset+i, "*");
                        offset++;
                    }
                }
            }
        }

        private void parseToSubTermsAndExpressions()
        {
            Console.WriteLine(startString);
            string currentStr = "";
            mathOperators prevOp = mathOperators.PLUS;

            if (isOperator(startString[0]))
            {
                prevOp = Program.strToOperator[startString[0]];
                startString = startString.Substring(1);
            }

            int skipToIndex = -1;
            bool prevWasSubTerm = false;
            bool prevOpChanged = false;

            for (int i=0; i< startString.Length; i++)
            {
                if (i < skipToIndex)
                    continue;

                char c = startString[i];

                if (charIsMinusButForNumericValue(i) && currentStr.Equals(""))
                {
                    currentStr = currentStr + c;
                    continue;
                }

                if (isOperator(c))
                {
                    if (movingToHigherLevelOperation(prevOp, Program.strToOperator[c]) && prevOpChanged)
                    {
                        int endOfSubTermIndex = findEndOfMultiplicativeChain(startString.Substring(i + 2))+1;
                        skipToIndex = i + endOfSubTermIndex;
                        string subTermStr = currentStr + startString.Substring(i, endOfSubTermIndex+1);
                        addNestedSubTerm(subTermStr, prevOp);
                        prevOp = Program.strToOperator[c];
                        prevOpChanged = true;

                        prevWasSubTerm = true;
                        continue;
                    }
                    else if (prevWasSubTerm == false)
                    {
                        subTerms.Add(new Expression(prevOp, currentStr));
                        prevOpChanged = true;

                    }

                    prevOp = Program.strToOperator[c];
                    currentStr = "";
                    prevWasSubTerm = false;
                } 
                else  if (c == '(')
                {
                    int endOfSubTermIndex = findRespectiveClosingBracketIndex(startString.Substring(i+1), i);
                    skipToIndex = i+endOfSubTermIndex;
                    string subTermStr = startString.Substring(i+1, endOfSubTermIndex-1);
                    addNestedSubTerm(subTermStr, prevOp);

                    currentStr = "";
                    prevWasSubTerm = true;
                } 
                else
                {
                    currentStr = currentStr + c;
                }
            }
        }

        private bool movingToHigherLevelOperation(mathOperators opBefore, mathOperators opAfter)
        {
            return (opBefore == mathOperators.PLUS || opBefore == mathOperators.MINUS) && (opAfter == mathOperators.MUL || opAfter == mathOperators.DIV);
        }

        private void addNestedSubTerm(string subTermStr, mathOperators subTermMOp)
        {
            subTerm nestedSubTerm = new subTermImp(subTermStr, subTermMOp);
            nestedSubTerm.parse();
            nestedSubTerm.simplify();
            subTerms.Add(nestedSubTerm);
        }

        private int findEndOfMultiplicativeChain(string substr)
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
                    if ((c == '-' || c == '+') && !charIsMinusButForNumericValue(substr, i))
                        return i;
                }
            }
            return substr.Length - 1;
        }

        private int findRespectiveClosingBracketIndex(string substr, int beginningFromI)
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
                    return i+1; // offset because zero based
                }
            }
            throw new InvalidTermException($"Opening brace at Index {beginningFromI} was never closed");
        }

        private bool charIsMinusButForNumericValue(int i)
        {
            return charIsMinusButForNumericValue(startString, i);
        }

        private bool charIsMinusButForNumericValue(string str, int i)
        {
            if (i > 0)
            {
                if (str[i] == '-' && (str[i - 1] == '*' || str[i - 1] == '/' || str[i - 1] == '('))
                    return true;
            }
            return false;
        }

        private bool isOperator(char c)
        {
            return new List<char>(Program.strToOperator.Keys).Contains(c);
        }
        private void applyDistributiveAndAssociativeLaw()
        {
            int offset = 0;
            
            for (int i = 0; i < subTerms.Count-1; i++)
            {
                var stCurrentObj = subTerms[offset+i];
                var stNextObj = subTerms[offset+i+1];
                bool hasOtherSubTermToBeMultiplied = checkSubTermAtIndexForMulDiv(offset+i+2);
                if (stCurrentObj is subTerm)
                {
                    subTerm stCurrent = (subTerm)stCurrentObj;

                    // associative law
                    if ((stCurrent.getMOp() == mathOperators.PLUS || stCurrent.getMOp() == mathOperators.MINUS) && !hasOtherSubTermToBeMultiplied)
                    {
                        offset = mergeByAssociativeLaw(offset, i, stCurrent, true);
                    }

                    // skipping if is +- but has other subterm to be multiplied is ok, because itll merge them in the next iteration

                    // distributive law
                    else if (stCurrent.getMOp() == mathOperators.MUL || stCurrent.getMOp() == mathOperators.DIV)
                    {
                        List<Expression> simplifiedTermsOfNew = new();

                        if (stNextObj is subTerm)
                        {
                            subTerm stNext = (subTerm)stNextObj;

                            foreach (Expression expr in stNext.getSimplifiedTerms())
                            {
                                simplifiedTermsOfNew.AddRange(multiplySubtermWithExpression(expr, stCurrent));
                            }

                            // mOp of subterm2 doesnt need to be paid attention to because it mustve already been */
                            // offset doesnt need to change: subterm will potentially be merged with the next subterm, or will be merged with the lowest level subterm

                        } else
                        {
                            Expression expr = (Expression)stNextObj;
                            simplifiedTermsOfNew.AddRange(multiplySubtermWithExpression(expr, stCurrent));
                        }

                        removeBothCurrentSubTermItems(offset, i);
                        mergeByAssociativeLawOrMergeIntoNewSubTerm(offset, i, simplifiedTermsOfNew, stCurrent.getMOp(), false);
                        offset--; // check the same subterm again, to apply one of both rules
                    }
                }

                if (stNextObj is subTerm && offset+i+1+1>subTerms.Count-1) // if stNextObj is last item, second +1 to check for next iteration
                {
                    //Console.WriteLine("associative law for last subterm triggered");
                    subTerm stNext = (subTerm)stNextObj;
                    offset = mergeByAssociativeLaw(offset, i, stNext, true);
                    break; // this is dangerous but works
                }
                Console.WriteLine(offset);
                printInfo("Merge Step", ToStringSubTerms());
            }
        }

        private List<Expression> multiplySubtermWithExpression(Expression expr, subTerm stNext)
        {
            List<Expression> simplifiedTermsOfNew = new();

            foreach (Expression expr2 in stNext.getSimplifiedTerms())
            {
                Expression newExpr = new Expression(expr);
                newExpr.combineWith(makeSimplifiedExpressionMultipliable(expr2, stNext.getMOp()));
                simplifiedTermsOfNew.Add(newExpr);
            }
            return simplifiedTermsOfNew;
        }

        private int mergeByAssociativeLawOrMergeIntoNewSubTerm(int offset, int i, List<Expression> simplifiedTermsOfNew, mathOperators mOp, bool withRemoving)
        {
            subTerm newSubTerm = new subTermImp(simplifiedTermsOfNew, mOp);
            if (offset + i + 1 < subTerms.Count - 1)
            {
                subTerms.Insert(offset + i, newSubTerm);
            }
            else
            {
                offset = mergeByAssociativeLaw(offset, i, newSubTerm, withRemoving);
            }
            return offset;
        }

        private void removeBothCurrentSubTermItems(int offset, int i)
        {
            subTerms.RemoveAt(offset + i);
            subTerms.RemoveAt(offset + i);
        }

        private int mergeByAssociativeLaw(int offset, int i, subTerm stNext, bool asAlreadyInsertedCaller)
        {   
            if (asAlreadyInsertedCaller)
            {
                subTerms.RemoveAt(offset + i);
            }
            int extraOffset = asAlreadyInsertedCaller ? 1 : 0;
            subTerms.InsertRange(offset + i + extraOffset, stNext.getSimplifiedTerms());
            offset += stNext.getSimplifiedTerms().Count - 1;
            return offset;
        }

        private Expression makeSimplifiedExpressionMultipliable(Expression expr, mathOperators bracesOp)
        {
            mathOperators origNumericValueOperator = expr.numericValue < 0 ? mathOperators.MINUS : mathOperators.PLUS;
            mathOperators numericValueOperator = Program.mergeLowLevelOperators(expr.mOp, origNumericValueOperator);

            if ((origNumericValueOperator == mathOperators.PLUS) != (numericValueOperator == mathOperators.PLUS))
            {
                expr.numericValue *= -1;
            }

            expr.mOp = bracesOp;
            return expr;
        }

        private bool checkSubTermAtIndexForMulDiv(int i)
        {
            if (i < subTerms.Count - 1)
            {
                if (subTerms[i] is subTerm)
                {
                    subTerm st = (subTerm)subTerms[i];
                    return st.getMOp() == mathOperators.MUL || st.getMOp() == mathOperators.DIV;
                }
            }
            return false;
        }

        public void simplify()
        {

            // assumption: every nested subterm has been simplified to expressions
            List<Expression> nextExpressionBatch = subTerms.Cast<Expression>().ToList();
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
                    if (currentSimplifierExpression.isCompatibleWith(expr2))
                    {
                        currentSimplifierExpression.combineWith(expr2);
                        printInfo($"Simplification step", ToStringSubTerms());
                    }
                    else
                    {
                        nextExpressionBatch.Add(expr2);
                    }
                }
                bool conditionToAppend = !(double.Parse(Program.neutralElementsByOperatorAsStr[currentSimplifierExpression.mOp]).Equals(currentSimplifierExpression.numericValue)
                    && currentSimplifierExpression.basis == "");

                if (conditionToAppend)
                    simplifiedTerm.Add(currentSimplifierExpression);
            }
            while (nextExpressionBatch.Count != 0);

        }

        public List<Expression> getSimplifiedTerms()
        {
            return simplifiedTerm;
        }

        public mathOperators getMOp()
        {
            return mOp;
        }

        private void printInfo(string description, string termStr)
        {
            if (makeInfoPrints)
                Console.WriteLine(Program.padStr(description, padLen) + termStr);
        }

        private string ToStringSubTerms()
        {
            string str = "";
            foreach (var e in subTerms)
            {
                str += e.ToString();
            }
            return finalizeAsString(str);
        }

        public override string ToString()
        {
            string str = ToStringNotNested();
            return finalizeAsString(str);
        }

        public string ToStringNotNested()
        {
            string str = "";
            foreach (Expression e in simplifiedTerm)
            {
                str += e.ToString();
            }
            return str;
        }

        private string finalizeAsString(string str)
        {
            char op = Program.strToOperator.FirstOrDefault(x => x.Value == mOp).Key;
            str = str.Replace("$", "");
            str = op + "(" + str + ")";
            return str;
        }
    }

    public class Expression
    {
        public mathOperators mOp;
        public double numericValue = 1.0;
        public string basis = "";
        public string divisorBasis = "";

        public Expression(mathOperators mOp, string strExpr)
        {
            if (strExpr.Length == 0)
                throw new InvalidMExpressionException("Empty string for Expression");

            this.mOp = mOp;

            bool subPartIsNumeric = false;
            int separationIndex = strExpr.Length+1;

            while (!subPartIsNumeric)
            {
                separationIndex--;
                subPartIsNumeric = double.TryParse(strExpr.Substring(0, separationIndex), out numericValue);

                if (separationIndex == 0)
                    break;
            }

            setBasis(strExpr.Substring(separationIndex));
            if (basis != "")
            {
                if (!basis.All(Char.IsLetter))
                    throw new InvalidMExpressionException($"Variable names may only be numeric: {basis}");
            }
        }

        public Expression(Expression expr)
        {
            mOp = expr.mOp;
            numericValue = expr.numericValue;
            basis = expr.basis;
            divisorBasis = expr.divisorBasis;
        }

            public bool isCompatibleWith(Expression expr2)
        {
            if (expr2.mOp == mathOperators.PLUS || expr2.mOp == mathOperators.MINUS)
                return basis.Equals(expr2.basis) && divisorBasis.Equals(expr2.divisorBasis);
            else if (expr2.mOp == mathOperators.MUL)
                return true;
            else if (expr2.mOp == mathOperators.DIV)
                return true;
            else
                throw new NotImplementedException($"mathOperator {expr2.mOp} has not yet been implemented in Expression.isCompatibleWith()");
        }

        public void combineWith(Expression expr2)
        {
            string prevBasis = basis;
            // assumption: bases are equal, or one of them has no basis - assumption: checked with isCompatibleWith
            numericValue = Program.opToNumReducerFn[expr2.mOp](numericValue, expr2.numericValue);

            if (expr2.mOp == mathOperators.MUL)
            {
                setBasis(basis + expr2.basis);
                setDivisorBasis(divisorBasis + expr2.divisorBasis);
            }
            else if (expr2.mOp == mathOperators.DIV)
            {
                setDivisorBasis(divisorBasis + expr2.basis);
                setBasis(basis + expr2.divisorBasis);
            }

            cancelOutOpposingBases();

        }

        private void cancelOutOpposingBases()
        {
            var regex = new Regex(Regex.Escape(""));
            foreach (char c in basis)
            {
                if (divisorBasis.Contains(c))
                {
                    setBasis(regex.Replace(basis, c.ToString(), 1));
                    setDivisorBasis(regex.Replace(divisorBasis, c.ToString(), 1));
                }
            }
        }

        public void setBasis(string basis)
        {
            this.basis = String.Concat(basis.OrderBy(c => c));
        }

        public void setDivisorBasis(string divisorBasis)
        {
            this.divisorBasis = String.Concat(divisorBasis.OrderBy(c => c));
        }
        
        public override string ToString()
        {
            char op = Program.strToOperator.FirstOrDefault(x => x.Value == mOp).Key;
            string numericValueStr = "";
            string divisorBasisStr = "";
            if (numericValue != 1.0 || basis == "")
            {
                numericValueStr = $"{numericValue}";
            }
            if (divisorBasis.Length != 0)
            {
                divisorBasisStr += $"/{divisorBasis}";
            }
            return $"{op}{numericValueStr}{basis}{divisorBasisStr}";
        }
    }

}
