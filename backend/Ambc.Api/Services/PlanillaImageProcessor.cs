using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Ambc.Api.Services;

/// <summary>
/// Procesa la imagen de la planilla: resize a 768px de ancho + contraste 1.2.
/// Sin binarización ni grayscale (según el reglamento).
/// </summary>
public static class PlanillaImageProcessor
{
    public static byte[] Procesar(byte[] input)
    {
        using var img = Image.Load(input);

        // Resize a 1500px de ancho (mantiene proporción)
        // 1500px permite leer las 160 casillas numeradas + tabla de cuartos.
        if (img.Width > 1500)
        {
            var ratio = 1500.0 / img.Width;
            var newHeight = (int)(img.Height * ratio);
            img.Mutate(x => x.Resize(1500, newHeight));
        }

        // Contraste 1.2
        img.Mutate(x => x.Contrast(1.2f));

        // Exportar JPEG
        using var ms = new MemoryStream();
        img.Save(ms, new JpegEncoder { Quality = 85 });
        return ms.ToArray();
    }
}
