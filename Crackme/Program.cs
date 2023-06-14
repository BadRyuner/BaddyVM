using Terminal.Gui;

namespace Crackme;

public unsafe class Program : Window
{
	TextField field;
	TextField field1;
	TextField field2;
	TextField field3;
	Button button;
	long wrongs = 0;

	public Program()
	{
		Title = "CrackMe by BadRyuner";
		/*
		var pin = new Label("Enter pin:") { X = Pos.Center() };

		field = new TextField("") { Y = Pos.Bottom(pin) + 1, Width = Dim.Percent(24f) };
		field1 = new TextField("") { X = Pos.Right(field) + 2, Y = Pos.Bottom(pin) + 1, Width = Dim.Percent(24f) };
		field2 = new TextField("") { X = Pos.Right(field1) + 2, Y = Pos.Bottom(pin) + 1, Width = Dim.Percent(24f) };
		field3 = new TextField("") { X = Pos.Right(field2) + 2, Y = Pos.Bottom(pin) + 1, Width = Dim.Percent(24f) };
		*/
		//button = new Button("Check") { X = Pos.Center(), Y = Pos.Bottom(field) + 1 };
		button = new Button("Check");
		Add(button);
		//button.Clicked += Test;

		//Add(pin);
		//Add(field);
		//Add(field1);
		//Add(field2);
		//Add(field3);
	}

	private void Test()
	{
		wrongs++;
		MessageBox.Query("Err", "Wrong Pin! Try again...", "Ok");
	}

	private static void Main(string[] args)
	{
		Application.Run<Program>();
		Application.Shutdown();
	}
}
