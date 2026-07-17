using System;
using System.Collections.Generic;
using Windows.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.System;
using Windows.Storage.Streams;
using Graphing;
using GraphControl;

namespace GraphControl
{
    public partial class Equation : DependencyObject, INotifyPropertyChanged
    {
        // Dependency Properties
        public static readonly DependencyProperty ExpressionProperty = DependencyProperty.Register(nameof(Expression), typeof(string), typeof(Equation), new PropertyMetadata(string.Empty, OnExpressionChanged));
        public static readonly DependencyProperty IsLineEnabledProperty = DependencyProperty.Register(nameof(IsLineEnabled), typeof(bool), typeof(Equation), new PropertyMetadata(true, OnIsLineEnabledChanged));
        public static readonly DependencyProperty IsValidatedProperty = DependencyProperty.Register(nameof(IsValidated), typeof(bool), typeof(Equation), new PropertyMetadata(false));
        public static readonly DependencyProperty HasGraphErrorProperty = DependencyProperty.Register(nameof(HasGraphError), typeof(bool), typeof(Equation), new PropertyMetadata(false));
        public static readonly DependencyProperty IsInequalityProperty = DependencyProperty.Register(nameof(IsInequality), typeof(bool), typeof(Equation), new PropertyMetadata(false));
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(Equation), new PropertyMetadata(false, OnIsSelectedChanged));
        public static readonly DependencyProperty EquationStyleProperty = DependencyProperty.Register(nameof(EquationStyle), typeof(EquationLineStyle), typeof(Equation), new PropertyMetadata(EquationLineStyle.Solid, OnEquationStyleChanged));
        public static readonly DependencyProperty GraphErrorTypeProperty = DependencyProperty.Register(nameof(GraphErrorType), typeof(ErrorType), typeof(Equation), new PropertyMetadata(ErrorType.Syntax));
        public static readonly DependencyProperty GraphErrorCodeProperty = DependencyProperty.Register(nameof(GraphErrorCode), typeof(int), typeof(Equation), new PropertyMetadata(0));
        public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(nameof(LineColor), typeof(Color), typeof(Equation), new PropertyMetadata(Colors.Black, OnLineColorChanged));
        // Property wrappers
        public string Expression { get => (string)GetValue(ExpressionProperty); set => SetValue(ExpressionProperty, value); }
        public bool IsLineEnabled { get => (bool)GetValue(IsLineEnabledProperty); set => SetValue(IsLineEnabledProperty, value); }
        public bool IsValidated { get => (bool)GetValue(IsValidatedProperty); set => SetValue(IsValidatedProperty, value); }
        public bool HasGraphError { get => (bool)GetValue(HasGraphErrorProperty); set => SetValue(HasGraphErrorProperty, value); }
        public bool IsInequality { get => (bool)GetValue(IsInequalityProperty); set => SetValue(IsInequalityProperty, value); }
        public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
        public EquationLineStyle EquationStyle { get => (EquationLineStyle)GetValue(EquationStyleProperty); set => SetValue(EquationStyleProperty, value); }
        public ErrorType GraphErrorType { get => (ErrorType)GetValue(GraphErrorTypeProperty); set => SetValue(GraphErrorTypeProperty, value); }
        public int GraphErrorCode { get => (int)GetValue(GraphErrorCodeProperty); set => SetValue(GraphErrorCodeProperty, value); }
        public Color LineColor { get => (Color)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }
        internal Graphing.IEquation? GraphedEquation { get; set; }

        private static void OnExpressionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(Expression));
        }

        private static void OnIsLineEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(IsLineEnabled));
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(IsSelected));
        }

        private static void OnEquationStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            //equation.OnPropertyChanged(nameof(EquationStyle));
        }

        private static void OnLineColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(LineColor));
        }

        public string? GetRequest()
        {
            if (string.IsNullOrEmpty(Expression))
                return null;
            string request;
            IsInequality = false;
            // Check for inequality symbols
            if (Expression.Contains("&#x3E;") || Expression.Contains("&#x3C;") || Expression.Contains("&#x2265;") || Expression.Contains("&#x2264;") || Expression.Contains(">") || Expression.Contains("<") || Expression.Contains("\u2265") || Expression.Contains("\u2264") || Expression.Contains("&lt;") || Expression.Contains("&gt;"))
            {
                request = "<mrow><mi>plotIneq2D</mi><mfenced separators=\"\">";
                IsInequality = true;
                EquationStyle = EquationLineStyle.Dash;
            }
            else if (Expression.Contains(">=<"))
            {
                request = "<mrow><mi>plotEq2d</mi><mfenced separators=\"\">";
            }
            // If the expression contains both x and y but no equal or inequality sign
            else if (Expression.Contains(">x<") && Expression.Contains(">y<"))
            {
                return null;
            }
            else
            {
                request = "<mrow><mi>plot2d</mi><mfenced separators=\"\">";
            }

            request += GetCleanExpression();
            request += "</mfenced></mrow>";
            return request;
        }

        private string GetCleanExpression()
        {
            string mathML = Expression;
            // Remove "mml:" prefix
            return mathML.Replace("mml:", "");
        }

        public bool IsGraphableEquation()
        {
            return !string.IsNullOrEmpty(Expression) && IsLineEnabled && !HasGraphError;
        }

        public Equation()
        {
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
