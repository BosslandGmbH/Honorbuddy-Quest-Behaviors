#region Usings
using System;
using System.Text;

using Honorbuddy.QuestBehaviorCore;
using Styx.CommonBot.Profiles.Quest.Order;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public class UserDefinedExpression<T>
    {
        private UserDefinedExpression(string expressionName, string expressionAsString)
        {
            Contract.Requires(!string.IsNullOrEmpty(expressionName), (context) => "expressionName may not be null or empty");
            Contract.Requires(!string.IsNullOrEmpty(expressionAsString), (context) => "expression may not be null or empty");

            // We hang onto the string representation of the expression, so we can display
            // meaningful error messages.
            ExpressionName = expressionName;
            ExpressionAsString = expressionAsString;

            // The factory that creates this is expected to compile it for us...
            // E.g., The user may have created the expression with or without argument passing.
            _compiledExpression = null;
        }

        public string ExpressionName { get; private set; }
        public string ExpressionAsString { get; private set; }

        private Func<T> _compiledExpression;

        // Factories...
        /// <summary>
        /// This factory creates an evaluatable method that returns type T from the USERPROVIDEDEXPRESSION.
        /// The EXPRESSIONNAME is used to display meaningful error messages at both 'compile' and 'evaluation'
        /// time, should the expression have problems.
        /// </summary>
        /// <param name="expressionName"></param>
        /// <param name="userProvidedExpression"></param>
        /// <returns></returns>
        public static UserDefinedExpression<T> NoArgsFactory(string expressionName, string userProvidedExpression)
        {
            var fullLambdaExpression = "() => " + userProvidedExpression;

            var userDefinedExpression = new UserDefinedExpression<T>(expressionName, userProvidedExpression);

            userDefinedExpression._compiledExpression = userDefinedExpression.Compile(fullLambdaExpression);
            if (userDefinedExpression._compiledExpression != null)
                return userDefinedExpression;

            return null;
        }


        private Func<T> Compile(string fullLambdaExpression)
        {
            Contract.Requires(!string.IsNullOrEmpty(fullLambdaExpression),
                              (context) => "fullLambdaExpression may not be null or empty");

            var expressionSet = new ExpressionSet();

            var expression = expressionSet.Add<Func<T>>(fullLambdaExpression);

            ExpressionError[] expressionErrors;
            if (expressionSet.Compile(out expressionErrors))
                return expression;

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

            QBCLog.ProfileError(builder.ToString());
            return null;
        }


        public T Evaluate()
        {
            try
            {
                // Expression evaluation can fail, so we must guard against this...
                // For instance, the user writes an sub-expression that can be null, and doesn't allow for it.
                // E.g., WoWSpell.FromId(12345).Cooldown, where the FromId() may be null.
                return _compiledExpression();
            }
            catch (Exception ex)
            {
                QBCLog.Fatal("Expression \"{1}\" evaluation failed."
                    + "  Failed to allow for null return values?{0}"
                    + "    Expression: {2}{0}"
                    + "    Exception: {3}", 
                    Environment.NewLine, ExpressionName, ExpressionAsString,
                    ex.ToString().Replace(Environment.NewLine, Environment.NewLine + "        "));
                throw;
            }
        }


        public override string ToString()
        {
            return ExpressionAsString;
        }
    }
}
