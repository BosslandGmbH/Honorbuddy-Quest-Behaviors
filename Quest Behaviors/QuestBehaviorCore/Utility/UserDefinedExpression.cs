#region Usings
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot.Profiles.Quest.Order;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{

    public abstract class UserDefinedExpressionBase 
    {
        protected UserDefinedExpressionBase(string expressionName, string expressionAsString, params string[] parameterNames)
        {
            Contract.Requires(!string.IsNullOrEmpty(expressionName), (context) => "expressionName may not be null or empty");
            Contract.Requires(!string.IsNullOrEmpty(expressionAsString), (context) => "expression may not be null or empty");

            // We hang onto the string representation of the expression, so we can display
            // meaningful error messages.
            ExpressionName = expressionName;
            ExpressionAsString = expressionAsString;
            ParameterNames = parameterNames;

            // The derived class that creates this is expected to compile it for us...
            CompiledExpression = null;
        }

        public bool HasErrors { get; protected set; }
        public string ExpressionName { get; private set; }
        public string ExpressionAsString { get; private set; }
        public string[] ParameterNames { get; private set; }

        protected Delegate CompiledExpression { get; set; }

        /// <summary>
        /// Compiles the specified full lambda expression.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate.</typeparam>
        /// <param name="fullLambdaExpression">The full lambda expression.</param>
        /// <returns><c>true</c> if compiled successfully; <c>false</c> otherwise.</returns>
        protected bool Compile<TDelegate>(string fullLambdaExpression) where TDelegate : class 
        {
            Contract.Requires(!string.IsNullOrEmpty(fullLambdaExpression),
                              (context) => "fullLambdaExpression may not be null or empty");

            var expressionSet = new ExpressionSet();

            CompiledExpression = (Delegate)(object)expressionSet.Add<TDelegate>(fullLambdaExpression);

            ExpressionError[] expressionErrors;
            if (expressionSet.Compile(out expressionErrors))
                return true;

            HasErrors = true;

            var builder = new StringBuilder();

            builder.AppendFormat("The \"{1}\" expression ({2}) does not compile.{0}",
                                Environment.NewLine,
                                ExpressionName, ExpressionAsString);

            if (expressionErrors.Length > 0)
                builder.AppendLine("Expression errors:");

            foreach (var errorDetail in expressionErrors)
            {
                builder.Append("    * ");
                builder.AppendLine(errorDetail.Error);
            }

            QBCLog.Error("{0}",builder.ToString());
            return false;
        }

        protected string BuildEvalErrorString(Exception ex)
        {
            return string.Format(
                "Expression \"{1}\" evaluation failed."
                + "  Failed to allow for null return values?{0}"
                + "    Expression: {2}{0}"
                + "    Parameters: {3}{0}"
                + "    Exception: {4}",
                Environment.NewLine,
                ExpressionName,
                ExpressionAsString,
                ParameterNames.Any() ? string.Join(", ", ParameterNames) : "(NONE)",
                ex.ToString().Replace(Environment.NewLine, Environment.NewLine + "        "));
        }

        public override string ToString()
        {
            return ExpressionAsString;
        }
    }

    public class UserDefinedExpression<TResult> : UserDefinedExpressionBase
    {
        public UserDefinedExpression(string expressionName, string expressionAsString)
            : base(expressionName, expressionAsString)
        {
            var fullLambdaExpression = "() => " + expressionAsString;

            Compile<Func<TResult>>(fullLambdaExpression);
        }

        public TResult Evaluate()
        {
            try
            {
                // Expression evaluation can fail, so we must guard against this...
                // For instance, the user writes an sub-expression that can be null, and doesn't allow for it.
                // E.g., WoWSpell.FromId(12345).Cooldown, where the FromId() may be null.
                return ((Func<TResult>)CompiledExpression)();
            }
            catch (Exception ex)
            {
                QBCLog.Fatal(BuildEvalErrorString(ex));
                throw;
            }
        }
    }

    public class UserDefinedExpression<TArg, TResult> : UserDefinedExpressionBase
    {
        public UserDefinedExpression(string expressionName, string expressionAsString, string parameterName)
            : base(expressionName, expressionAsString, parameterName)
        {
            var fullLambdaExpression = parameterName + " => " + expressionAsString;

            Compile<Func<TArg, TResult>>(fullLambdaExpression);
        }

        public TResult Evaluate(TArg arg)
        {
            try
            {
                // Expression evaluation can fail, so we must guard against this...
                // For instance, the user writes an sub-expression that can be null, and doesn't allow for it.
                // E.g., WoWSpell.FromId(12345).Cooldown, where the FromId() may be null.
                return ((Func<TArg, TResult>)CompiledExpression)(arg);
            }
            catch (Exception ex)
            {
                QBCLog.Fatal(BuildEvalErrorString(ex));
                throw;
            }
        }
    }

    public class UserDefinedExpression<TArg1, TArg2, TResult> : UserDefinedExpressionBase
    {
        public UserDefinedExpression(
            string expressionName,
            string expressionAsString,
            string parameterName1,
            string parameterName2)
            : base(expressionName, expressionAsString, parameterName1, parameterName2)
        {
            var fullLambdaExpression = string.Format("({0}, {1}) => {2}", parameterName1, parameterName2, expressionAsString);

            Compile<Func<TArg1, TArg2, TResult>>(fullLambdaExpression);
        }

        public TResult Evaluate(TArg1 arg1, TArg2 arg2)
        {
            try
            {
                // Expression evaluation can fail, so we must guard against this...
                // For instance, the user writes an sub-expression that can be null, and doesn't allow for it.
                // E.g., WoWSpell.FromId(12345).Cooldown, where the FromId() may be null.
                return ((Func<TArg1, TArg2, TResult>)CompiledExpression)(arg1, arg2);
            }
            catch (Exception ex)
            {
                QBCLog.Fatal(BuildEvalErrorString(ex));
                throw;
            }
        }
    }
}
