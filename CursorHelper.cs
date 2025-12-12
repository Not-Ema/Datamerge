using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Reflection;

namespace Datamerge
{
    public static class CursorHelper
    {
        public static readonly DependencyProperty CursorProperty =
            DependencyProperty.RegisterAttached(
                "Cursor",
                typeof(InputSystemCursorShape),
                typeof(CursorHelper),
                new PropertyMetadata(InputSystemCursorShape.Arrow, OnCursorChanged));

        public static void SetCursor(DependencyObject element, InputSystemCursorShape value)
        {
            element.SetValue(CursorProperty, value);
        }

        public static InputSystemCursorShape GetCursor(DependencyObject element)
        {
            return (InputSystemCursorShape)element.GetValue(CursorProperty);
        }

        private static void OnCursorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                var cursorShape = (InputSystemCursorShape)e.NewValue;
                var propertyInfo = typeof(UIElement).GetProperty(
                    "ProtectedCursor",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (propertyInfo != null)
                {
                    var cursor = InputSystemCursor.Create(cursorShape);
                    propertyInfo.SetValue(element, cursor);
                }
            }
        }
    }
}