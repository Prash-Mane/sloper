﻿using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SloperMobile.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class HomePage : ContentPage
    {
        public HomePage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
        }
    }
}
