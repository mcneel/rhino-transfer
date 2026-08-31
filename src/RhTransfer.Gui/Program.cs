using System;

using Eto.Forms;

using RhTransfer.Core.Infrastructure;

namespace RhTransfer.Gui;

public static class Program
{

  [STAThread]
  public static void Main()
  {
    new Application(Eto.Platform.Detect).Run(new MainForm(RhinoRoot.ForCurrentUser()));
  }

}
