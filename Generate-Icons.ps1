param([Parameter(Mandatory=$true)][string]$OutputDirectory)

# Offline renderer for the four checked-in simple SVGs, using Windows/.NET only.
# This is intentionally not a general SVG engine. Unsupported input is rejected.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName PresentationCore
$sourceDirectory = Join-Path $PSScriptRoot 'Icons\Source'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$invariant = [Globalization.CultureInfo]::InvariantCulture

function Number([string]$value, [double]$fallback = 0) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $fallback }
    return [double]::Parse($value, $invariant)
}

function Read-Icon([string]$name, [int]$size) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create((Join-Path $sourceDirectory $name), $settings)
    $xml = [Xml.XmlDocument]::new()
    $xml.XmlResolver = $null
    try { $xml.Load($reader) } finally { $reader.Dispose() }
    $svg = $xml.DocumentElement
    $view = $svg.GetAttribute('viewBox').Trim() -split '[,\s]+'
    if ($view.Length -ne 4) { throw "Invalid SVG viewBox: $name" }
    $width = Number $view[2]
    $height = Number $view[3]
    if ($width -le 0 -or $height -le 0) { throw "Invalid SVG size: $name" }
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    try {
        $context.PushTransform([Windows.Media.ScaleTransform]::new($size / $width, $size / $height))
        $context.PushTransform([Windows.Media.TranslateTransform]::new(-(Number $view[0]), -(Number $view[1])))
        foreach ($node in $svg.ChildNodes) {
            if ($node.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
            $transform = $node.GetAttribute('transform')
            if ($transform -and $transform -ne 'translate(0 0)') { throw "Unsupported SVG transform in $name" }
            $fill = $node.GetAttribute('fill')
            if ($node.GetAttribute('style') -match '(?:^|;)\s*fill\s*:\s*([^;]+)') { $fill = $Matches[1].Trim() }
            if (!$fill) { $fill = '#000000' }
            if ($fill -eq 'none') { continue }
            $color = [Windows.Media.ColorConverter]::ConvertFromString($fill)
            $brush = [Windows.Media.SolidColorBrush]::new($color)
            switch ($node.LocalName) {
                'path' {
                    $rule = 'F1 '
                    if ($node.GetAttribute('fill-rule') -eq 'evenodd') { $rule = 'F0 ' }
                    $geometry = [Windows.Media.Geometry]::Parse($rule + $node.GetAttribute('d'))
                    $context.DrawGeometry($brush, $null, $geometry)
                }
                'rect' {
                    $rectangle = [Windows.Rect]::new((Number $node.GetAttribute('x')), (Number $node.GetAttribute('y')),
                        (Number $node.GetAttribute('width')), (Number $node.GetAttribute('height')))
                    $rx = Number $node.GetAttribute('rx')
                    $ry = Number $node.GetAttribute('ry') $rx
                    $context.DrawRoundedRectangle($brush, $null, $rectangle, $rx, $ry)
                }
                'circle' {
                    $center = [Windows.Point]::new((Number $node.GetAttribute('cx')), (Number $node.GetAttribute('cy')))
                    $radius = Number $node.GetAttribute('r')
                    $context.DrawEllipse($brush, $null, $center, $radius, $radius)
                }
                default { throw "Unsupported SVG element in ${name}: $($node.LocalName)" }
            }
        }
        $context.Pop()
        $context.Pop()
    }
    finally { $context.Close() }
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()
    return $bitmap
}

function Save-IconPng([Windows.Media.Imaging.BitmapSource]$bitmap, [string]$path) {
    # SOLIDWORKS CommandGroup uses 256-color image strips. WPF's automatically
    # computed palette discards alpha, and AlphaThreshold=0 maps no pixels to
    # transparency. Reserve an explicit transparent entry before conversion.
    $optimized = [Windows.Media.Imaging.BitmapPalette]::new($bitmap, 255)
    $colors = [Collections.Generic.List[Windows.Media.Color]]::new()
    $colors.Add([Windows.Media.Color]::FromArgb(0, 0, 0, 0))
    foreach ($color in $optimized.Colors) {
        $colors.Add([Windows.Media.Color]::FromArgb(255, $color.R, $color.G, $color.B))
    }
    $palette = [Windows.Media.Imaging.BitmapPalette]::new($colors)
    $indexed = [Windows.Media.Imaging.FormatConvertedBitmap]::new($bitmap, [Windows.Media.PixelFormats]::Indexed8, $palette, 50)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($indexed))
    $stream = [IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

foreach ($size in @(20, 32, 40, 64, 96, 128)) {
    $icons = @()
    foreach ($name in @('OrcaSlicer.svg', 'BambuStudio.svg', 'PrusaSlicer.svg', 'Settings.svg')) {
        $icons += Read-Icon $name $size
    }
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    try {
        for ($i = 0; $i -lt 4; $i++) {
            $context.DrawImage($icons[$i], [Windows.Rect]::new($i * $size, 0, $size, $size))
        }
    }
    finally { $context.Close() }
    $strip = [Windows.Media.Imaging.RenderTargetBitmap]::new(4 * $size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $strip.Render($visual)
    Save-IconPng $strip (Join-Path $OutputDirectory "toolbar_$size.png")
    Save-IconPng $icons[3] (Join-Path $OutputDirectory "main_$size.png")
    Write-Host "Original application icons: $size px (Orca, Bambu, Prusa, Settings)"
}
