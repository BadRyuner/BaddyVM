using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
	// WARNING!!!
	//
	// CASTS BROKEN!!! Use Unsafe.As
	//
	// WARNING!!!

	public MainWindow()
	{
		InitializeComponent();
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		var but = Unsafe.As<Button>(sender);
		Pin.Content = Unsafe.As<string>(Pin.Content) + Unsafe.As<string>(but.Content);
	}

	private void Button_Click_Clear(object sender, RoutedEventArgs e)
	{
		Pin.Content = string.Empty;
	}

	private void Button_Click_Check(object sender, RoutedEventArgs e)
	{
		var pass = Unsafe.As<string>(Pin.Content);
		if (pass == "337812345")
			MessageBox.Show("You win!", "Awesome!");
		else
			MessageBox.Show("Wrong pass :(", "Bad");
	}
}
