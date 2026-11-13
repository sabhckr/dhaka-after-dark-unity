// Trailer look tools (Stage 1 audit, 2026-11-03). macOS frameworks only: no downloads.
//
//   look contact <out.png> <columns> <cellWidth> <in.png>...   labelled contact sheet
//   look grade <in.png> <out.png> <compare.png>                 grade-only look test v1
//
// The grade changes colour and tone only: contrast, split toning (blue shadows, violet mids,
// cyan highlights), a bloom of pixels that are already bright, a vignette and fine grain. It adds
// no light source, sign, neon, vehicle or structure, so every edge in the output is an edge the
// game rendered. Inputs are never written. Build (CLT, no Xcode licence needed):
//   /Library/Developer/CommandLineTools/usr/bin/swiftc -O \
//     -sdk /Library/Developer/CommandLineTools/SDKs/MacOSX26.sdk -target arm64-apple-macosx14.0 \
//     Tools/trailer/look.swift -o <scratch>/look
import AppKit
import CoreImage
import CoreText
import Foundation

func fail(_ m: String) -> Never { FileHandle.standardError.write((m + "\n").data(using: .utf8)!); exit(1) }

func loadCG(_ path: String) -> CGImage {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let img = CGImageSourceCreateImageAtIndex(src, 0, nil) else { fail("cannot read \(path)") }
    return img
}

func savePNG(_ img: CGImage, _ path: String) {
    let url = URL(fileURLWithPath: path)
    try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
    guard let dst = CGImageDestinationCreateWithURL(url as CFURL, "public.png" as CFString, 1, nil) else { fail("cannot write \(path)") }
    CGImageDestinationAddImage(dst, img, nil)
    if !CGImageDestinationFinalize(dst) { fail("cannot write \(path)") }
}

let srgb = CGColorSpace(name: CGColorSpace.sRGB)!

func context(_ w: Int, _ h: Int) -> CGContext {
    CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: srgb,
              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
}

func label(_ ctx: CGContext, _ text: String, x: CGFloat, y: CGFloat, size: CGFloat) {
    let font = CTFontCreateWithName("Helvetica-Bold" as CFString, size, nil)
    let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: CGColor(red: 1, green: 1, blue: 1, alpha: 1)]
    let line = CTLineCreateWithAttributedString(NSAttributedString(string: text, attributes: attrs))
    let w = CTLineGetTypographicBounds(line, nil, nil, nil)
    ctx.setFillColor(CGColor(red: 0, green: 0, blue: 0, alpha: 0.7))
    ctx.fill(CGRect(x: x, y: y, width: CGFloat(w) + 8, height: size + 6))
    ctx.textPosition = CGPoint(x: x + 4, y: y + 4)
    CTLineDraw(line, ctx)
}

func contact(_ out: String, _ cols: Int, _ cellW: Int, _ files: [String]) {
    let cellH = cellW * 9 / 16
    let rows = (files.count + cols - 1) / cols
    let ctx = context(cols * cellW, rows * cellH)
    ctx.setFillColor(CGColor(red: 0.1, green: 0.1, blue: 0.1, alpha: 1))
    ctx.fill(CGRect(x: 0, y: 0, width: cols * cellW, height: rows * cellH))
    ctx.interpolationQuality = .high
    for (i, f) in files.enumerated() {
        let x = (i % cols) * cellW, y = (rows - 1 - i / cols) * cellH
        ctx.draw(loadCG(f), in: CGRect(x: x, y: y, width: cellW, height: cellH))
        let name = URL(fileURLWithPath: f).deletingPathExtension().lastPathComponent
        label(ctx, "\(i + 1) \(name)", x: CGFloat(x + 2), y: CGFloat(y + 2), size: 13)
    }
    savePNG(ctx.makeImage()!, out)
}

func smooth(_ a: Float, _ b: Float, _ x: Float) -> Float {
    let t = min(max((x - a) / (b - a), 0), 1)
    return t * t * (3 - 2 * t)
}

/// Grade v1 on 8-bit sRGB pixels. Hue-preserving S-curve, 20 % desaturation, split toning.
func gradePixels(_ img: CGImage) -> CGImage {
    let w = img.width, h = img.height
    let ctx = context(w, h)
    ctx.draw(img, in: CGRect(x: 0, y: 0, width: w, height: h))
    let p = ctx.data!.bindMemory(to: UInt8.self, capacity: w * h * 4)
    var seed: UInt32 = 0x9E3779B9
    for i in 0..<(w * h) {
        var r = Float(p[i * 4]) / 255, g = Float(p[i * 4 + 1]) / 255, b = Float(p[i * 4 + 2]) / 255
        let l = max(0.2126 * r + 0.7152 * g + 0.0722 * b, 1e-4)
        // Contrast: an S-curve on luma, applied as a ratio so hue stays put.
        let lc: Float = l < 0.5 ? 0.5 * pow(2 * l, 1.2) : 1 - 0.5 * pow(2 * (1 - l), 1.2)
        let k = lc / l * 1.25 // exposure lift: the night renders sit low (K-24, no tone mapping)
        r *= k; g *= k; b *= k
        // 20 % towards grey.
        r = lc + (r - lc) * 0.8; g = lc + (g - lc) * 0.8; b = lc + (b - lc) * 0.8
        // Split toning by luma: deep blue shadows, violet mids, cyan highlights.
        let ws = 1 - smooth(0.0, 0.42, lc), wh = smooth(0.5, 1.0, lc), wm = max(0, 1 - ws - wh)
        r += ws * -0.015 + wm * 0.030 + wh * -0.035
        g += ws * 0.004 + wm * -0.012 + wh * 0.030
        b += ws * 0.060 + wm * 0.040 + wh * 0.045
        // Black floor lifted towards blue, then fine grain (+-1.5/255).
        r = r * 0.97 + 0.006; g = g * 0.97 + 0.010; b = b * 0.97 + 0.026
        seed = seed &* 1664525 &+ 1013904223
        let n = (Float(seed >> 8) / Float(1 << 24) - 0.5) * 3 / 255
        p[i * 4] = UInt8(min(max((r + n) * 255, 0), 255))
        p[i * 4 + 1] = UInt8(min(max((g + n) * 255, 0), 255))
        p[i * 4 + 2] = UInt8(min(max((b + n) * 255, 0), 255))
    }
    return ctx.makeImage()!
}

func grade(_ input: String, _ out: String, _ compare: String) {
    let src = loadCG(input)
    let toned = CIImage(cgImage: gradePixels(src))
    let extent = toned.extent
    // Bloom only from pixels that are already bright (luma over about 0.55), tinted cool.
    let bright = toned.applyingFilter("CIColorMatrix", parameters: [
        "inputRVector": CIVector(x: 0.2126, y: 0.7152, z: 0.0722, w: 0), "inputGVector": CIVector(x: 0.2126, y: 0.7152, z: 0.0722, w: 0),
        "inputBVector": CIVector(x: 0.2126, y: 0.7152, z: 0.0722, w: 0), "inputBiasVector": CIVector(x: -0.55, y: -0.55, z: -0.55, w: 0)])
        .applyingFilter("CIColorClamp")
        .applyingFilter("CIColorMatrix", parameters: [
            "inputRVector": CIVector(x: 0.9, y: 0, z: 0, w: 0), "inputGVector": CIVector(x: 0, y: 1.6, z: 0, w: 0),
            "inputBVector": CIVector(x: 0, y: 0, z: 2.0, w: 0)])
        .clampedToExtent().applyingFilter("CIGaussianBlur", parameters: ["inputRadius": 14]).cropped(to: extent)
    let bloomed = bright.applyingFilter("CIAdditionCompositing", parameters: ["inputBackgroundImage": toned])
    let vignetted = bloomed.applyingFilter("CIVignette", parameters: ["inputIntensity": 0.55, "inputRadius": 1.6])
    let ci = CIContext(options: [.workingColorSpace: srgb])
    guard let graded = ci.createCGImage(vignetted, from: extent, format: .RGBA8, colorSpace: srgb) else { fail("render failed") }
    savePNG(graded, out)
    // Side by side, original left, graded right, at half size each.
    let w = src.width / 2, h = src.height / 2
    let ctx = context(w * 2, h)
    ctx.interpolationQuality = .high
    ctx.draw(src, in: CGRect(x: 0, y: 0, width: w, height: h))
    ctx.draw(graded, in: CGRect(x: w, y: 0, width: w, height: h))
    label(ctx, "ORIGINAL (game render)", x: 6, y: CGFloat(h - 24), size: 14)
    label(ctx, "LOOK TEST v1 (grade only)", x: CGFloat(w + 6), y: CGFloat(h - 24), size: 14)
    savePNG(ctx.makeImage()!, compare)
}

let a = CommandLine.arguments
guard a.count >= 2 else { fail("usage: look contact|grade ...") }
switch a[1] {
case "contact" where a.count >= 6: contact(a[2], Int(a[3])!, Int(a[4])!, Array(a[5...]))
case "grade" where a.count == 5: grade(a[2], a[3], a[4])
default: fail("usage: look contact <out> <cols> <cellW> <in>... | look grade <in> <out> <compare>")
}
