using HomeCamMonitor;
using System.Text.Json;
namespace V2Isolation;
internal static partial class Check
{
    private static void TestLogoVariants()
    {
        if (new Settings().LogoVariant != 0) throw new Exception("Default logo changed.");
        for (var variant = 0; variant < 3; variant++)
        {
            var settings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { LogoVariant = variant }))!;
            if (settings.LogoVariant != variant) throw new Exception("Logo variant did not round-trip.");
            if (SettingsReset.Apply(settings, SettingsResetScope.Display).LogoVariant != 0) throw new Exception("Display reset did not restore default logo.");
            using var bitmap = LogoVariants.LoadImage(variant);
            using var icon = LogoVariants.LoadIcon(variant);
            var visible = 0;
            for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A == 0) continue;
                visible++;
                if (variant == 1 && (pixel.R != 255 || pixel.G != 255 || pixel.B != 255)) throw new Exception("White transparent logo contains other colors.");
                if (variant == 2 && (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)) throw new Exception("Black transparent logo contains other colors.");
            }
            if (visible < 1000 || icon.Width != 32) throw new Exception("Logo/icon is incomplete.");
            if (variant > 0 && bitmap.GetPixel(0,0).A != 0) throw new Exception("Transparent logo background is opaque.");
            using var form = new SettingsForm(settings, SettingsLocation.DefaultFolder);
            static IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[]{x}.Concat(All(x)));
            var picker = All(form).OfType<ComboBox>().Single(c => c.Name == "LogoVariant");
            if (picker.Items.Count != 3 || picker.SelectedIndex != variant) throw new Exception("Settings picker does not restore three variants.");
            form.ShowInTaskbar = false; form.Opacity = 0; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
            form.Show(); Application.DoEvents();
            var appearance = All(form).Single(c => c.Name == "CameraAppearance");
            appearance.Dock = DockStyle.None; appearance.AutoSize = false; appearance.Size = new Size(1050, 110);
            appearance.PerformLayout();
            using (var preview = new Bitmap(appearance.Width, appearance.Height))
            {
                appearance.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                var output = Path.Combine(Environment.CurrentDirectory, "store-artifacts");
                Directory.CreateDirectory(output);
                preview.Save(Path.Combine(output, $"logo-settings-{variant}.png"));
            }
            picker.SelectedIndex = (variant + 1) % 3;
            if (All(form).OfType<PictureBox>().Single(c => c.Name == "LogoVariantPreview").Image is null) throw new Exception("Preview is missing.");
        }
        Console.WriteLine("PASS: three embedded logo/icon variants, alpha/color checks, settings round-trip, reset and settings selection/preview.");
    }
}
