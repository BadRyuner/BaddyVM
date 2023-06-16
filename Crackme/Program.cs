using System.Runtime.InteropServices;
using Terminal.Gui;

namespace Crackme;

public unsafe class Program
{
	static Button Verify;

	public Program()
	{

	}

	Button b;
	Button c;
	Button e;
	long a = 88;
	byte h = 2;
	public TextField ff;

	public void Write()
	{
		Console.WriteLine(a);
		Console.WriteLine(h);
	}

	private static void Main(string[] args)
	{
		Start();
	}

	private static void Start()
	{
		Application.Init();
		var window = new Window("Crackme by BadRyuner") { X = Pos.Center(), Y = Pos.Center(), Width = 42, Height = 10 };
		window.ColorScheme.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);

		Label p = null;
		var labels = new Label[8];
		for (int i = 0; i < 8; i++)
		{
			if (p == null)
				p = new Label("0") { X = 1, Y = 3, TextAlignment = TextAlignment.Centered, Width = 3 };
			else
				p = new Label("0") { X = Pos.Right(p) + 2, Y = 3, TextAlignment = TextAlignment.Centered, Width = 3 };
			p.Border = new Border() { BorderStyle = BorderStyle.Rounded, BorderBrush = Color.White, Background = Color.Black };
			labels[i] = p;
			window.Add(p);
			var iCopy = i;
			var up = new Button("\u2191") { X = p.X - 1, Y = 1 };
			up.Border = new Border() { BorderBrush = Color.White, Background = Color.Black };
			up.Clicked += () =>
			{
				var current = labels[iCopy].Text[0] - '0';
				if (current < 9)
					labels[iCopy].Text = ((char)(current + 1 + '0')).ToString();
				else
					labels[iCopy].Text = "0";
			};
			var down = new Button("\u2193") { X = p.X - 1, Y = 5 };
			down.Border = new Border() { BorderBrush = Color.White, Background = Color.Black };
			down.Clicked += () =>
			{
				var current = labels[iCopy].Text[0] - '0';
				if (current > 0)
					labels[iCopy].Text = ((char)(current - 1 + '0')).ToString();
				else
					labels[iCopy].Text = "9";
			};
			window.Add(up, down);
		}

		var button = new Button("Open") { X = Pos.Center(), Y = 7 };
		button.Clicked += () => 
		{ 
			bool good = false;
			long pin = 0;
			byte* p = (byte*)&pin;
			p[0] = (byte)(labels[0].Text[0] - '0');
			p[1] = (byte)(labels[1].Text[0] - '0');
			if (*(ushort*)p != 0b0000_0011_0000_0110)
				goto ohno;
			p[2] = (byte)(labels[2].Text[0] - '0');
			p[3] = (byte)(labels[3].Text[0] - '0');
			p[4] = (byte)(labels[4].Text[0] - '0');
			p[5] = (byte)(labels[5].Text[0] - '0');
			p[6] = (byte)(labels[6].Text[0] - '0');
			p[7] = (byte)(labels[7].Text[0] - '0');
			if (pin == 285881730728710)
				good = true;
			ohno:
			if (good) // 6 3 2 7 2 4 1 0
				MessageBox.Query("Mission Completed!", "Please write a solution on crackmes.one :)", "Ok", "Nah");
			else
				MessageBox.Query("Bad Pin!", "Try Again!", "Meh...");
			
		};

		window.Add(button);
		Application.Top.Add(window);
		Application.Run();
		Application.Shutdown();
	}
}
