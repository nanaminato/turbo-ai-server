# Third-party notices

## PdfPig 0.1.12

`Turbo.Kit` uses [PdfPig](https://github.com/UglyToad/PdfPig) to extract PDF text. PdfPig is distributed under the Apache License, Version 2.0. The package replaces the legacy `iTextSharp` dependency, whose .NET Framework-only restore assets generated a `NU1701` compatibility warning for this .NET 10 project.

The previous NPOI dependency was removed. DOCX text extraction now uses the Open XML package format (`ZipArchive` and `XDocument`) provided by .NET, so the project no longer needs to accept NPOI's OSMF EULA.

## Captcha rendering

The ImageSharp, ImageSharp.Drawing and Fonts dependencies have been removed. The verification endpoint generates a self-contained SVG using .NET XML APIs, with no server font installation or graphics license required. Its JSON response retains `img` (base64-encoded bytes) and `code`, and adds `contentType: image/svg+xml`. Clients must use the returned content type when constructing the image data URL.
