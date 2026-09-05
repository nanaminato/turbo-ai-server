# Third-party notices

## PdfPig 0.1.12

`Turbo-Kit` uses [PdfPig](https://github.com/UglyToad/PdfPig) to extract PDF text. PdfPig is distributed under the Apache License, Version 2.0. The package replaces the legacy `iTextSharp` dependency, whose .NET Framework-only restore assets generated a `NU1701` compatibility warning for this .NET 10 project.

The previous NPOI dependency was removed. DOCX text extraction now uses the Open XML package format (`ZipArchive` and `XDocument`) provided by .NET, so the project no longer needs to accept NPOI's OSMF EULA.
