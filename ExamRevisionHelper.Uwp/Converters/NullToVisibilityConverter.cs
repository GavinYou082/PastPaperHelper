﻿using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace ExamRevisionHelper.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
         => value == null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, string language) => null;
    }
}
