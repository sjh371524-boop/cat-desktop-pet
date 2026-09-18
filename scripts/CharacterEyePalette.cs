using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

// Mechanical palette propagation of the approved ImageGen iris edit. Never
// resample a sprite: only colored iris pixels inside audited eye ROIs may change.
public static class CharacterEyePalette
{
    private static readonly Dictionary<string, Rectangle> eyes = new Dictionary<string, Rectangle>();
    private static List<Color> palette;

    private static void Eye(string path, int x, int y, int width, int height)
    { eyes.Add(path.Replace('/', Path.DirectorySeparatorChar), new Rectangle(x,y,width,height)); }

    private static void RegisterEyes()
    {
        if (eyes.Count > 0) return;
        Eye("stand/000.png",161,65,25,25);
        foreach (string path in new [] {"drag/000.png","drag/002.png","lick/000.png","lick/002.png","pet/000.png","pet/002.png","sleep_transition/000.png","wake/002.png"}) Eye(path,161,65,25,25);
        Eye("drag/001.png",163,61,22,24); Eye("drag/003.png",160,66,26,26);
        Eye("drag_transition/left/000.png",132,71,24,25);
        Eye("drag_transition/left/001.png",90,70,25,26);
        Eye("drag_transition/left/002.png",81,79,26,26);
        Eye("drag_transition/left/003.png",14,109,25,27);
        Eye("drag_transition/right/000.png",158,70,24,25);
        Eye("drag_transition/right/001.png",172,68,22,26);
        Eye("drag_transition/right/002.png",187,77,18,27);
        // Side profiles facing right expose the OTHER eye; their source stays
        // yellow-green. The separately baked left profile exposes the blue eye.
        Eye("idle_1/paw_left/000.png",141,88,29,17);
        Eye("idle_1/paw_left/003.png",158,64,27,19);
        Eye("idle_1/paw_right/000.png",165,86,23,22);
        Eye("idle_1/paw_right/003.png",165,67,22,23);
        Eye("lick/001.png",165,70,27,17);
        for(int i=0;i<4;i++) { Eye("pet_gesture/left/00"+i+".png",111-i*2,73,29,26); Eye("pet_gesture/right/00"+i+".png",153,55,27,29); }
        Eye("sleep_transition/001.png",181,131,31,17);
        Eye("wake/001.png",171,150,29,23);
        Eye("wake/001b-transition-v1.png",182,63,27,28);

        // Retired artwork is updated too, but remains retired/unloaded.
        int[,] calculator={{177,70,28,17},{176,61,26,22},{172,65,28,21},{166,66,27,22},{163,57,26,22},{163,60,26,22},{169,63,27,24},{159,65,29,24}};
        for(int i=0;i<8;i++) Eye("calculator_greeting/left/00"+i+".png",calculator[i,0],calculator[i,1],calculator[i,2],calculator[i,3]);
        int[,] smooth={{160,77},{148,77},{130,73},{114,74},{161,69},{141,71},{126,73},{119,71}};
        for(int i=0;i<8;i++) Eye("pet_gesture/smooth_right/00"+i+".png",smooth[i,0],smooth[i,1],28,23);
        int[,] drowsy={{154,74},{155,80},{188,75},{162,90},{168,89},{172,111},{170,128}};
        for(int i=0;i<7;i++) Eye("soothe_sleep/drowsy/00"+i+".png",drowsy[i,0],drowsy[i,1],29,22);
        Eye("soothe_sleep/stretch/000.png",177,95,24,17);
        Eye("soothe_sleep/stretch/001.png",171,88,25,18);
        Eye("soothe_sleep/stretch/006.png",159,86,26,20);
        Eye("soothe_sleep/stretch/007.png",161,88,26,19);
        Eye("soothe_sleep/stretch_v2/000.png",164,94,28,21);
        Eye("soothe_sleep/stretch_v2/006.png",167,79,29,22);
        Eye("soothe_sleep/stretch_v2/007.png",145,76,29,22);
    }

    private static bool IsIris(Color c)
    {
        double hue=c.GetHue();
        return c.A > 0 && hue >= 43 && hue <= 165 && c.GetSaturation() > .22
            && c.G > 28 && c.B < c.G * .84;
    }

    private static Color Blue(Color c)
    {
        // Keep dark pupil pixels and white highlights exactly as authored.
        int index=(int)Math.Round(Math.Max(0, Math.Min(1, (Math.Max(c.R,c.G)-28)/215.0))*(palette.Count-1));
        Color p=palette[index];
        return Color.FromArgb(c.A,p.R,p.G,p.B);
    }

    private static Bitmap Edit(Bitmap source, Rectangle eye, out int count)
    {
        Bitmap result=(Bitmap)source.Clone(); count=0;
        for(int y=Math.Max(0,eye.Top);y<Math.Min(source.Height,eye.Bottom);y++)
        for(int x=Math.Max(0,eye.Left);x<Math.Min(source.Width,eye.Right);x++)
        {
            Color c=source.GetPixel(x,y);
            if(!IsIris(c)) continue;
            result.SetPixel(x,y,Blue(c)); count++;
        }
        return result;
    }

    private static void SaveOrVerify(Bitmap desired,string output,bool verify)
    {
        if(!verify) { Directory.CreateDirectory(Path.GetDirectoryName(output)); desired.Save(output,ImageFormat.Png); return; }
        using(Bitmap actual=new Bitmap(output))
        {
            if(actual.Size!=desired.Size) throw new Exception("Sprite dimensions changed: "+output);
            for(int y=0;y<actual.Height;y++) for(int x=0;x<actual.Width;x++)
                if(actual.GetPixel(x,y).ToArgb()!=desired.GetPixel(x,y).ToArgb())
                    throw new Exception("Unexpected pixel at "+x+","+y+": "+output);
        }
    }

    public static void Run(string root,bool verify)
    {
        RegisterEyes();
        string art=Path.Combine(root,"assets","character");
        string baseline=Path.Combine(root,"source-art","pre-heterochromia","character");
        string generated=Path.Combine(root,"template-input","cat-heterochromia-imagegen.png");
        palette=new List<Color>();
        using(Bitmap image=new Bitmap(generated))
        {
            // Sample only the blue iris of the ImageGen edit, not its backdrop.
            for(int y=(int)(image.Height*.26);y<image.Height*.35;y++)
            for(int x=(int)(image.Width*.64);x<image.Width*.73;x++)
            {
                Color c=image.GetPixel(x,y);
                if(c.GetHue()>180 && c.GetHue()<225 && c.B>c.R*1.17 && c.GetSaturation()>.18) palette.Add(c);
            }
        }
        palette=palette.Distinct().OrderBy(c=>c.R*.2126+c.G*.7152+c.B*.0722).ToList();
        if(palette.Count<10) throw new Exception("Approved ImageGen blue iris palette is missing.");
        int changed=0, audited=0;
        foreach(string sourcePath in Directory.GetFiles(baseline,"*.png",SearchOption.AllDirectories))
        {
            string relative=sourcePath.Substring(baseline.Length+1);
            if(Path.GetFileName(relative).Contains("sheet")) continue;
            using(Bitmap source=new Bitmap(sourcePath))
            {
                Rectangle roi; int count;
                using(Bitmap result=Edit(source,eyes.TryGetValue(relative,out roi)?roi:Rectangle.Empty,out count))
                {
                    if(eyes.ContainsKey(relative) && count<2) throw new Exception("Eye ROI missed its iris: "+relative+" ("+count+")");
                    SaveOrVerify(result,Path.Combine(art,relative),verify);
                    changed+=count; audited++;
                    Console.WriteLine(relative+": "+count+" iris pixels");
                }
            }
        }
        for(int i=0;i<4;i++)
        {
            string name=i.ToString("000")+".png";
            using(Bitmap source=new Bitmap(Path.Combine(baseline,"drag_side","open",name)))
            {
                int n; using(Bitmap left=Edit(source,new Rectangle(207,108,39,42),out n))
                {
                    if(n<2) throw new Exception("Side-profile iris not found.");
                    left.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    SaveOrVerify(left,Path.Combine(art,"drag_side","left_open",name),verify);
                }
            }
            using(Bitmap blink=new Bitmap(Path.Combine(baseline,"drag_side","blink",name)))
            { blink.RotateFlip(RotateFlipType.RotateNoneFlipX); SaveOrVerify(blink,Path.Combine(art,"drag_side","left_blink",name),verify); }
        }
        // All full-color source sheets are regenerated from their updated cells;
        // original ImageGen exports stay recoverable only in source-art.
        string[] sheets={"calculator_greeting/calculator-greeting-sheet-v1.png|calculator_greeting/left",
            "calculator_greeting/calculator-greeting-sheet-v2.png|calculator_greeting/left",
            "drag_side/drag-side-sheet-v2-rgba.png|drag_side/open|drag_side/blink",
            "pet_gesture/pet-gesture-sheet-v2-rgba.png|pet_gesture/right|pet_gesture/left",
            "pet_gesture/pet-gesture-smooth-sheet-v3-chroma.png|pet_gesture/smooth_right",
            "soothe_sleep/drowsy-sheet-v1.png|soothe_sleep/drowsy",
            "soothe_sleep/stretch-sheet-v1.png|soothe_sleep/stretch",
            "soothe_sleep/stretch-sheet-v2-chroma.png|soothe_sleep/stretch_v2"};
        foreach(string spec in sheets)
        {
            string[] parts=spec.Split('|');
            using(Bitmap sheet=new Bitmap(1024,512,PixelFormat.Format32bppArgb))
            {
                using(Graphics g=Graphics.FromImage(sheet))
                {
                    int slot=0;
                    for(int d=1;d<parts.Length;d++)
                    foreach(string frame in Directory.GetFiles(Path.Combine(art,parts[d].Replace('/',Path.DirectorySeparatorChar)),"*.png").OrderBy(p=>p,StringComparer.Ordinal))
                    { using(Bitmap b=new Bitmap(frame)) g.DrawImageUnscaled(b,(slot%4)*256,(slot/4)*256); slot++; }
                }
                SaveOrVerify(sheet,Path.Combine(art,parts[0].Replace('/',Path.DirectorySeparatorChar)),verify);
            }
        }
        using(Bitmap canonical=new Bitmap(Path.Combine(art,"stand","000.png")))
        {
            SaveOrVerify(canonical,Path.Combine(root,"template-input","pixel-calico-heterochromia.png"),verify);
            SaveOrVerify(canonical,Path.Combine(art,"reference","pixel-calico.png"),verify);
        }
        Console.WriteLine("PASS: "+audited+" frames checked; "+changed+" iris pixels, silhouette/alpha/other pixels preserved; directional eyes and 8 sheets checked.");
    }
}
