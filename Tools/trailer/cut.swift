// Trailer cut renderer (trailer v1, 2026-11-03). macOS frameworks only (CoreImage, CoreText, AVFoundation).
//
//   cut render <edl.json> <out.mov> [fromSecond toSecond]   picture only, H.264, from the edit list
//   cut still <edl.json> <second> <out.png>                  one frame, for review
//   cut mux <video.mov> <audio.wav> <out.mov|out.mp4>        picture + sound (mov: passthrough; mp4: H.264 + AAC)
//   cut probe <movie>                                        durations, frame rate, audio format
//   cut pcm <movie> <out.wav>                                decoded sound as float WAV (to meter the delivery)
//   cut grab <movie> <outPrefix> <second ...>                decoded frames from a finished movie
//
// The edit list (Production/Trailer_2026/Editing/edl_*.json) holds clips (a treated still with a move
// from/to [centreX, centreY, zoom], fades, rain, shake, flicker, dim, and whether it carries artistic
// additions, which shows the concept label), texts (narration, cards, title, credits) and flashes.
// Fonts are the OFL Bengali families in Art/Fonts (they carry Latin too).
// Build: swiftc -O -swift-version 5 -sdk <CLT>/SDKs/MacOSX26.sdk -target arm64-apple-macosx14.0 cut.swift -o cut
import AppKit
import AVFoundation
import CoreImage
import CoreText
import Foundation

struct Clip: Codable { var img: String; var t: Double; var d: Double; var fin: Double?; var fout: Double?; var from: [Double]; var to: [Double]; var rain: Double?; var shake: Double?; var flicker: Double?; var label: Bool?; var dim: Double? }
struct TextItem: Codable { var t: Double; var d: Double; var style: String; var text: String; var size: Double? }
struct Flash: Codable { var t: Double; var d: Double; var peak: Double }
/// `layout` overrides text placement for other formats (vertical, thumbnail): first baselines in pixels from the bottom
/// (narrY, cardY, titleY, axiomY, creditY, smallY, labelY) and labelSize. Without it, the 2.39:1 letterbox layout applies.
struct EDL: Codable { var name: String; var fps: Int; var width: Int; var height: Int; var duration: Double; var bars: Int; var imageRoot: String; var label: String; var clips: [Clip]; var texts: [TextItem]; var flashes: [Flash]; var layout: [String: Double]? }

func fail(_ m: String) -> Never { FileHandle.standardError.write((m + "\n").data(using: .utf8)!); exit(1) }

let root = ProcessInfo.processInfo.environment["DAD_ROOT"] ?? FileManager.default.currentDirectoryPath
let srgb = CGColorSpace(name: CGColorSpace.sRGB)!

func hashf(_ a: Int, _ b: Int) -> Double {
    var h = UInt64(bitPattern: Int64(a &* 73856093 ^ b &* 19349663))
    h ^= h >> 33; h = h &* 0xff51afd7ed558ccd; h ^= h >> 33; h = h &* 0xc4ceb9fe1a85ec53; h ^= h >> 33
    return Double(h & 0xFFFFFF) / Double(0xFFFFFF)
}

/// Smooth noise in about -1...1 from a sum of incommensurate sines.
func wobble(_ t: Double, _ k: Double) -> Double { (sin(t * 1.7 + k) + 0.6 * sin(t * 3.1 + 2 * k) + 0.35 * sin(t * 5.3 + 3 * k)) / 1.95 }

// MARK: fonts and text
func font(_ file: String, _ size: CGFloat) -> CTFont {
    let url = URL(fileURLWithPath: root + "/Art/Fonts/" + file) as CFURL
    guard let descs = CTFontManagerCreateFontDescriptorsFromURL(url) as? [CTFontDescriptor], let d = descs.first else { fail("font \(file)") }
    return CTFontCreateWithFontDescriptor(d, size, nil)
}

final class Typesetter {
    let w: Int, h: Int, bars: Int, layout: [String: Double]
    init(w: Int, h: Int, bars: Int, layout: [String: Double]) { self.w = w; self.h = h; self.bars = bars; self.layout = layout }

    func y(_ key: String, _ fallback: CGFloat) -> CGFloat { layout[key].map { CGFloat($0) } ?? fallback }

    /// Draws centred lines; `y` is the first baseline from the bottom of the frame. Lines wider than 92% of the frame
    /// shrink the whole block (font, tracking and line gap) to fit.
    func image(lines: [String], font f0: CTFont, kern k0: CGFloat, alpha: CGFloat, y: CGFloat, lineGap g0: CGFloat, shadow: Bool) -> CIImage {
        func lineFor(_ s: String, _ f: CTFont, _ k: CGFloat) -> CTLine {
            CTLineCreateWithAttributedString(NSAttributedString(string: s, attributes: [.font: f, .foregroundColor: CGColor(red: 1, green: 1, blue: 1, alpha: alpha), .kern: k]))
        }
        let widest = lines.map { CGFloat(CTLineGetTypographicBounds(lineFor($0, f0, k0), nil, nil, nil)) }.max() ?? 0
        let fit = min(1, CGFloat(w) * 0.92 / max(widest, 1))
        let f = fit < 1 ? CTFontCreateCopyWithAttributes(f0, CTFontGetSize(f0) * fit, nil, nil) : f0
        let kern = k0 * fit, lineGap = g0 * fit
        let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: srgb, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        if shadow { ctx.setShadow(offset: .zero, blur: 14, color: CGColor(red: 0, green: 0, blue: 0, alpha: 0.85)) }
        for (i, s) in lines.enumerated() {
            let line = lineFor(s, f, kern)
            let width = CTLineGetTypographicBounds(line, nil, nil, nil)
            ctx.textPosition = CGPoint(x: (CGFloat(w) - CGFloat(width)) / 2, y: y - CGFloat(i) * lineGap)
            CTLineDraw(line, ctx)
        }
        return CIImage(cgImage: ctx.makeImage()!)
    }

    func glow(_ img: CIImage, _ r: CGFloat, _ g: CGFloat, _ b: CGFloat, radius: Double) -> CIImage {
        img.clampedToExtent().applyingFilter("CIGaussianBlur", parameters: ["inputRadius": radius])
            .cropped(to: img.extent)
            .applyingFilter("CIColorMatrix", parameters: [
                "inputRVector": CIVector(x: r, y: 0, z: 0, w: 0), "inputGVector": CIVector(x: 0, y: g, z: 0, w: 0),
                "inputBVector": CIVector(x: 0, y: 0, z: b, w: 0), "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 0.9)])
    }

    func render(_ t: TextItem) -> CIImage {
        let H = CGFloat(h), B = CGFloat(bars), m = CGFloat(t.size ?? 1)
        let lines = t.text.components(separatedBy: "\n")
        switch t.style {
        case "narr":
            // In the lower letterbox bar, like release subtitles: readable over any shot. Without bars it needs a shadow.
            return image(lines: lines, font: font("tirobangla/TiroBangla-Regular.ttf", 42 * m), kern: 0.5, alpha: 0.95, y: y("narrY", B / 2 - 12), lineGap: 54 * m, shadow: bars == 0)
        case "card":
            let txt = image(lines: lines, font: font("hindsiliguri/HindSiliguri-Bold.ttf", 60 * m), kern: 16 * m, alpha: 1, y: y("cardY", H / 2 - 20), lineGap: 70 * m, shadow: false)
            return txt.composited(over: glow(txt, 0.25, 0.85, 1.0, radius: 14))
        case "title":
            let txt = image(lines: lines, font: font("hindsiliguri/HindSiliguri-Bold.ttf", 104 * m), kern: 22 * m, alpha: 1, y: y("titleY", H / 2 + 10), lineGap: 110 * m, shadow: false)
            return txt.composited(over: glow(txt, 1.0, 0.2, 0.75, radius: 26).composited(over: glow(txt, 0.2, 0.9, 1.0, radius: 9)))
        case "axiom":
            let txt = image(lines: lines, font: font("hindsiliguri/HindSiliguri-Bold.ttf", 84 * m), kern: 42 * m, alpha: 1, y: y("axiomY", H / 2 + 30), lineGap: 90 * m, shadow: false)
            return txt.composited(over: glow(txt, 0.3, 0.9, 1.0, radius: 18))
        case "credit":
            return image(lines: lines, font: font("hindsiliguri/HindSiliguri-Regular.ttf", 27 * m), kern: 7 * m, alpha: 0.85, y: y("creditY", H / 2 - 60), lineGap: 42 * m, shadow: bars == 0)
        case "small":
            return image(lines: lines, font: font("hindsiliguri/HindSiliguri-Regular.ttf", 19 * m), kern: 1.5 * m, alpha: 0.7, y: y("smallY", H - B / 2 - 7), lineGap: 26 * m, shadow: bars == 0)
        default: fail("unknown text style \(t.style)")
        }
    }

    func label(_ s: String) -> CIImage {
        let size = CGFloat(layout["labelSize"] ?? 19)
        return image(lines: s.components(separatedBy: "\n"), font: font("hindsiliguri/HindSiliguri-Regular.ttf", size), kern: 2, alpha: 0.7, y: y("labelY", CGFloat(h) - CGFloat(bars) / 2 - 6), lineGap: size * 1.35, shadow: bars == 0)
    }
}

// MARK: the frame
final class Renderer {
    let e: EDL
    let W: CGFloat, H: CGFloat
    var cache: [String: CIImage] = [:]
    let texts: [CIImage]
    let labelImage: CIImage
    let black: CIImage
    let grainSource = CIFilter(name: "CIRandomGenerator")!.outputImage!

    init(_ e: EDL) {
        self.e = e
        W = CGFloat(e.width); H = CGFloat(e.height)
        let ts = Typesetter(w: e.width, h: e.height, bars: e.bars, layout: e.layout ?? [:])
        texts = e.texts.map { ts.render($0) }
        labelImage = ts.label(e.label)
        black = CIImage(color: CIColor(red: 0, green: 0, blue: 0)).cropped(to: CGRect(x: 0, y: 0, width: e.width, height: e.height))
    }

    /// The still scaled to cover the frame (16:9 stills fill a 16:9 frame exactly; a vertical frame crops the sides).
    func image(_ name: String) -> CIImage {
        if let c = cache[name] { return c }
        let url = URL(fileURLWithPath: root + "/" + e.imageRoot + "/" + name + ".png")
        guard let img = CIImage(contentsOf: url) else { fail("missing still \(url.path)") }
        let s = max(W / img.extent.width, H / img.extent.height)
        let scaled = img.transformed(by: CGAffineTransform(scaleX: s, y: s))
        cache[name] = scaled
        return scaled
    }

    func alpha(_ c: Clip, _ t: Double) -> Double {
        let a = t - c.t, b = c.t + c.d - t
        if a < 0 || b < 0 { return 0 }
        var v = 1.0
        if let fi = c.fin, fi > 0 { v = min(v, a / fi) }
        if let fo = c.fout, fo > 0 { v = min(v, b / fo) }
        return max(0, min(1, v))
    }

    func scale(_ img: CIImage, _ k: Double, alpha: Double = 1) -> CIImage {
        let a = CGFloat(k * alpha), al = CGFloat(alpha)
        return img.applyingFilter("CIColorMatrix", parameters: [
            "inputRVector": CIVector(x: a, y: 0, z: 0, w: 0), "inputGVector": CIVector(x: 0, y: a, z: 0, w: 0),
            "inputBVector": CIVector(x: 0, y: 0, z: a, w: 0), "inputAVector": CIVector(x: 0, y: 0, z: 0, w: al)])
    }

    func rain(_ amount: Double, seed: Int, t: Double) -> CIImage {
        let w = Int(W), h = Int(H)
        let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: srgb, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        ctx.setLineCap(.round)
        let n = Int(amount * 1100)
        for i in 0..<n {
            let x0 = hashf(i, seed) * Double(w + 200) - 100
            let speed = 1500 + 900 * hashf(i, seed + 1), len = 22 + 46 * hashf(i, seed + 2), y0 = hashf(i, seed + 3) * Double(h + 200)
            let near = hashf(i, seed + 4)
            let y = Double(h) + 100 - (y0 + speed * t).truncatingRemainder(dividingBy: Double(h + 200))
            let x = x0 - 0.13 * (Double(h) - y)
            ctx.setStrokeColor(CGColor(red: 0.75, green: 0.85, blue: 1.0, alpha: 0.07 + 0.2 * near))
            ctx.setLineWidth(0.8 + 1.4 * near)
            ctx.move(to: CGPoint(x: x, y: y)); ctx.addLine(to: CGPoint(x: x + 0.13 * len, y: y + len)); ctx.strokePath()
        }
        return CIImage(cgImage: ctx.makeImage()!)
    }

    func frame(_ t: Double, index: Int) -> CIImage {
        var out = black
        var labelAlpha = 0.0
        for (ci, c) in e.clips.enumerated() {
            let a = alpha(c, t)
            if a <= 0 { continue }
            let u0 = max(0, min(1, (t - c.t) / c.d))
            let u = 0.5 * u0 + 0.5 * u0 * u0 * (3 - 2 * u0)
            var cx = c.from[0] + (c.to[0] - c.from[0]) * u, cy = c.from[1] + (c.to[1] - c.from[1]) * u
            let z = c.from[2] + (c.to[2] - c.from[2]) * u
            if let sh = c.shake, sh > 0 { cx += sh * 0.006 * wobble(t * 1.3, Double(ci)); cy += sh * 0.006 * wobble(t * 1.1, Double(ci) + 4) }
            let img = image(c.img)
            let iw = Double(img.extent.width), ih = Double(img.extent.height)
            let hx = Double(W) / (2 * z * iw), hy = Double(H) / (2 * z * ih)   // half the view, as a fraction of the still
            cx = min(max(cx, hx), 1 - hx); cy = min(max(cy, hy), 1 - hy)
            let px = CGFloat(cx * iw), py = CGFloat((1 - cy) * ih)
            let tr = CGAffineTransform(translationX: -px, y: -py).concatenating(CGAffineTransform(scaleX: CGFloat(z), y: CGFloat(z))).concatenating(CGAffineTransform(translationX: W / 2, y: H / 2))
            var k = 1.0 - (c.dim ?? 0)
            if let fl = c.flicker, fl > 0 {
                let f = Int(t * 24)
                let dip = hashf(f / 2, ci) > 0.93 ? 0.55 : (hashf(f, ci + 9) > 0.8 ? 0.15 : 0)
                k *= 1 - fl * (dip + 0.3 * (0.5 + 0.5 * wobble(t * 7, Double(ci))))
            }
            var layer = scale(img.transformed(by: tr).cropped(to: black.extent), k, alpha: a)
            if let r = c.rain, r > 0 {
                layer = scale(rain(r, seed: ci * 31 + 7, t: t), 1, alpha: a).composited(over: layer)
            }
            out = layer.composited(over: out)
            if c.label == true { labelAlpha = max(labelAlpha, a) }
        }
        // Flashes: fast rise, exponential fall.
        for f in e.flashes where t >= f.t - 0.04 && t <= f.t + f.d + 0.3 {
            let x = t - f.t
            let v = x < 0 ? f.peak * (1 + x / 0.04) : f.peak * exp(-x / max(f.d, 0.01) * 2.2)
            out = scale(CIImage(color: CIColor(red: 0.85, green: 0.93, blue: 1)).cropped(to: black.extent), v, alpha: v).composited(over: out)
        }
        // Cards sit on a dimmed frame so they read over bright shots.
        var cardA = 0.0
        for tx in e.texts where tx.style == "card" && t >= tx.t - 0.1 && t <= tx.t + tx.d + 0.2 {
            cardA = max(cardA, min(1, min((t - tx.t + 0.1) / 0.15, (tx.t + tx.d + 0.2 - t) / 0.35)))
        }
        if cardA > 0 { out = scale(out, 1 - 0.82 * cardA) }
        // Grain (CI works in linear light: multiplicative, so blacks stay black, plus a faint floor), vignette.
        let gx = CGFloat(hashf(index, 1) * 4000), gy = CGFloat(hashf(index, 2) * 4000)
        let noise = grainSource.transformed(by: CGAffineTransform(translationX: -gx, y: -gy)).cropped(to: black.extent)
        let grain = noise.applyingFilter("CIColorMatrix", parameters: [
                "inputRVector": CIVector(x: 0.16, y: 0, z: 0, w: 0), "inputGVector": CIVector(x: 0.16, y: 0, z: 0, w: 0),
                "inputBVector": CIVector(x: 0.16, y: 0, z: 0, w: 0), "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 0),
                "inputBiasVector": CIVector(x: 0.92, y: 0.92, z: 0.92, w: 1)])
        let floor = noise.applyingFilter("CIColorMatrix", parameters: [
                "inputRVector": CIVector(x: 0, y: 0.002, z: 0, w: 0), "inputGVector": CIVector(x: 0, y: 0.002, z: 0, w: 0),
                "inputBVector": CIVector(x: 0, y: 0.002, z: 0, w: 0), "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 0),
                "inputBiasVector": CIVector(x: -0.0008, y: -0.0008, z: -0.0008, w: 1)])
        out = grain.applyingFilter("CIMultiplyCompositing", parameters: ["inputBackgroundImage": out]).cropped(to: black.extent)
        out = floor.applyingFilter("CIAdditionCompositing", parameters: ["inputBackgroundImage": out]).cropped(to: black.extent)
        out = out.applyingFilter("CIVignetteEffect", parameters: ["inputCenter": CIVector(x: W / 2, y: H / 2), "inputRadius": max(W, H) * 0.42, "inputIntensity": 0.5, "inputFalloff": 0.65])
        // Without letterbox bars, text sits on the picture: shade the lower third and the top edge instead.
        if e.bars == 0 {
            func shade(_ y0: CGFloat, _ y1: CGFloat, _ a: CGFloat) -> CIImage {
                CIFilter(name: "CILinearGradient", parameters: ["inputPoint0": CIVector(x: 0, y: y0), "inputPoint1": CIVector(x: 0, y: y1),
                    "inputColor0": CIColor(red: 0, green: 0, blue: 0, alpha: a), "inputColor1": CIColor(red: 0, green: 0, blue: 0, alpha: 0)])!.outputImage!.cropped(to: black.extent)
            }
            out = shade(0, H * 0.34, 0.75).composited(over: shade(H, H * 0.86, 0.6)).composited(over: out)
        }
        // Letterbox to about 2.39:1.
        let bar = CGFloat(e.bars)
        let bars = CIImage(color: CIColor(red: 0, green: 0, blue: 0)).cropped(to: CGRect(x: 0, y: 0, width: W, height: bar))
            .composited(over: CIImage(color: CIColor(red: 0, green: 0, blue: 0)).cropped(to: CGRect(x: 0, y: H - bar, width: W, height: bar)))
        out = bars.composited(over: out)
        // The concept label shares the top bar with the opening and closing notes, so it yields to them.
        for tx in e.texts where tx.style == "small" && t >= tx.t - 0.3 && t <= tx.t + tx.d + 0.3 { labelAlpha = 0 }
        if labelAlpha > 0.01 { out = scale(labelImage, 1, alpha: labelAlpha).composited(over: out) }
        for (i, tx) in e.texts.enumerated() where t >= tx.t && t <= tx.t + tx.d {
            let fin = tx.style == "card" ? 0.12 : 0.45, fout = tx.style == "card" ? 0.25 : 0.5
            let a = max(0, min(1, min((t - tx.t) / fin, (tx.t + tx.d - t) / fout)))
            var img = texts[i]
            if tx.style == "title" || tx.style == "axiom" {
                let s = CGFloat(1 + 0.035 * (t - tx.t) / tx.d)
                img = img.transformed(by: CGAffineTransform(translationX: -W / 2, y: -H / 2).concatenating(CGAffineTransform(scaleX: s, y: s)).concatenating(CGAffineTransform(translationX: W / 2, y: H / 2)))
            }
            out = scale(img, 1, alpha: a).composited(over: out)
        }
        return out.cropped(to: black.extent)
    }
}

func loadEDL(_ path: String) -> EDL {
    do { return try JSONDecoder().decode(EDL.self, from: Data(contentsOf: URL(fileURLWithPath: path))) } catch { fail("edit list: \(error)") }
}

func render(_ edlPath: String, _ outPath: String, from: Double?, to: Double?) {
    let e = loadEDL(edlPath)
    let r = Renderer(e)
    let url = URL(fileURLWithPath: outPath)
    try? FileManager.default.removeItem(at: url)
    let writer = try! AVAssetWriter(outputURL: url, fileType: .mov)
    let settings: [String: Any] = [
        AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: e.width, AVVideoHeightKey: e.height,
        AVVideoColorPropertiesKey: [AVVideoColorPrimariesKey: AVVideoColorPrimaries_ITU_R_709_2, AVVideoTransferFunctionKey: AVVideoTransferFunction_ITU_R_709_2, AVVideoYCbCrMatrixKey: AVVideoYCbCrMatrix_ITU_R_709_2],
        AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 30_000_000, AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
                                          AVVideoExpectedSourceFrameRateKey: e.fps, AVVideoMaxKeyFrameIntervalKey: e.fps]]
    let input = AVAssetWriterInput(mediaType: .video, outputSettings: settings)
    input.expectsMediaDataInRealTime = false
    let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [
        kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA, kCVPixelBufferWidthKey as String: e.width, kCVPixelBufferHeightKey as String: e.height])
    writer.add(input)
    writer.startWriting()
    writer.startSession(atSourceTime: .zero)
    let ctx = CIContext(options: [.outputColorSpace: srgb])
    let total = Int((e.duration * Double(e.fps)).rounded())
    let f0 = Int(((from ?? 0) * Double(e.fps)).rounded()), f1 = min(total, Int(((to ?? e.duration) * Double(e.fps)).rounded()))
    let start = Date()
    for f in f0..<f1 {
        autoreleasepool {
            while !input.isReadyForMoreMediaData { usleep(2000) }
            var pb: CVPixelBuffer?
            CVPixelBufferPoolCreatePixelBuffer(nil, adaptor.pixelBufferPool!, &pb)
            let img = r.frame(Double(f) / Double(e.fps), index: f)
            ctx.render(img, to: pb!, bounds: img.extent, colorSpace: srgb)
            if !adaptor.append(pb!, withPresentationTime: CMTime(value: CMTimeValue(f - f0), timescale: CMTimeScale(e.fps))) { fail("append failed at frame \(f): \(String(describing: writer.error))") }
        }
        if f % 240 == 0 { print("frame \(f) of \(f1), \(String(format: "%.0f", Date().timeIntervalSince(start))) s") }
    }
    input.markAsFinished()
    let sem = DispatchSemaphore(value: 0)
    writer.finishWriting { sem.signal() }
    sem.wait()
    if writer.status != .completed { fail("writer: \(String(describing: writer.error))") }
    print("wrote \(f1 - f0) frames to \(outPath) in \(String(format: "%.0f", Date().timeIntervalSince(start))) s")
}

func still(_ edlPath: String, _ t: Double, _ outPath: String) {
    let e = loadEDL(edlPath)
    let r = Renderer(e)
    let img = r.frame(t, index: Int(t * Double(e.fps)))
    let ctx = CIContext(options: [.outputColorSpace: srgb])
    let cg = ctx.createCGImage(img, from: img.extent, format: .RGBA8, colorSpace: srgb)!
    let dst = CGImageDestinationCreateWithURL(URL(fileURLWithPath: outPath) as CFURL, "public.png" as CFString, 1, nil)!
    CGImageDestinationAddImage(dst, cg, nil)
    CGImageDestinationFinalize(dst)
}

func mux(_ video: String, _ audio: String, _ out: String) {
    let comp = AVMutableComposition()
    let va = AVURLAsset(url: URL(fileURLWithPath: video)), aa = AVURLAsset(url: URL(fileURLWithPath: audio))
    guard let vt = va.tracks(withMediaType: .video).first, let at = aa.tracks(withMediaType: .audio).first else { fail("no tracks") }
    let cv = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
    let ca = comp.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid)!
    do {
        try cv.insertTimeRange(CMTimeRange(start: .zero, duration: va.duration), of: vt, at: .zero)
        try ca.insertTimeRange(CMTimeRange(start: .zero, duration: CMTimeMinimum(aa.duration, va.duration)), of: at, at: .zero)
    } catch { fail("compose: \(error)") }
    let mp4 = out.hasSuffix(".mp4")
    // The 1920x1080 preset would shrink a portrait picture to fit 1080 high; portrait keeps its size via HighestQuality.
    let portrait = vt.naturalSize.height > vt.naturalSize.width
    let preset = !mp4 ? AVAssetExportPresetPassthrough : portrait ? AVAssetExportPresetHighestQuality : AVAssetExportPreset1920x1080
    guard let ex = AVAssetExportSession(asset: comp, presetName: preset) else { fail("no export session") }
    let url = URL(fileURLWithPath: out)
    try? FileManager.default.removeItem(at: url)
    ex.outputURL = url
    ex.outputFileType = mp4 ? .mp4 : .mov
    let sem = DispatchSemaphore(value: 0)
    ex.exportAsynchronously { sem.signal() }
    sem.wait()
    if ex.status != .completed { fail("export: \(String(describing: ex.error))") }
    print("wrote \(out)")
}

func probe(_ path: String) {
    let a = AVURLAsset(url: URL(fileURLWithPath: path))
    print("\(path): duration \(String(format: "%.4f", a.duration.seconds)) s")
    for t in a.tracks {
        let fd = (t.formatDescriptions as! [CMFormatDescription]).first
        var extra = ""
        if t.mediaType == .video { extra = "\(Int(t.naturalSize.width))x\(Int(t.naturalSize.height)) at \(t.nominalFrameRate) fps, \(Int((Double(t.nominalFrameRate) * t.timeRange.duration.seconds).rounded())) frames, \(Int(t.estimatedDataRate / 1000)) kbit/s" }
        if t.mediaType == .audio, let fd = fd, let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(fd)?.pointee {
            extra = "\(Int(asbd.mSampleRate)) Hz, \(asbd.mChannelsPerFrame) ch, \(Int(t.estimatedDataRate / 1000)) kbit/s"
        }
        var codec = "?"
        if let fd = fd {
            let st: UInt32 = CMFormatDescriptionGetMediaSubType(fd)
            let bytes: [UInt8] = [UInt8((st >> 24) & 255), UInt8((st >> 16) & 255), UInt8((st >> 8) & 255), UInt8(st & 255)]
            codec = String(decoding: bytes, as: UTF8.self)
        }
        print("  \(t.mediaType.rawValue) \(codec): \(String(format: "%.4f", t.timeRange.duration.seconds)) s; \(extra)")
    }
}

/// QC: decodes a movie's sound to 48 kHz stereo float WAV, so the delivered (AAC) audio can be metered.
func pcm(_ path: String, _ out: String) {
    let asset = AVURLAsset(url: URL(fileURLWithPath: path))
    guard let track = asset.tracks(withMediaType: .audio).first, let reader = try? AVAssetReader(asset: asset) else { fail("no audio in \(path)") }
    let output = AVAssetReaderTrackOutput(track: track, outputSettings: [
        AVFormatIDKey: kAudioFormatLinearPCM, AVSampleRateKey: 48000, AVNumberOfChannelsKey: 2,
        AVLinearPCMBitDepthKey: 32, AVLinearPCMIsFloatKey: true, AVLinearPCMIsNonInterleaved: false, AVLinearPCMIsBigEndianKey: false])
    reader.add(output)
    reader.startReading()
    var data = Data()
    while let sb = output.copyNextSampleBuffer() {
        guard let bb = CMSampleBufferGetDataBuffer(sb) else { continue }
        var len = 0; var ptr: UnsafeMutablePointer<CChar>?
        CMBlockBufferGetDataPointer(bb, atOffset: 0, lengthAtOffsetOut: nil, totalLengthOut: &len, dataPointerOut: &ptr)
        if let p = ptr { data.append(UnsafeRawPointer(p).assumingMemoryBound(to: UInt8.self), count: len) }
    }
    if reader.status != .completed { fail("decode: \(String(describing: reader.error))") }
    var h = Data()
    func u32(_ v: UInt32) { var x = v.littleEndian; h.append(Data(bytes: &x, count: 4)) }
    func u16(_ v: UInt16) { var x = v.littleEndian; h.append(Data(bytes: &x, count: 2)) }
    h.append("RIFF".data(using: .ascii)!); u32(UInt32(36 + data.count)); h.append("WAVEfmt ".data(using: .ascii)!)
    u32(16); u16(3); u16(2); u32(48000); u32(48000 * 8); u16(8); u16(32)
    h.append("data".data(using: .ascii)!); u32(UInt32(data.count))
    try! (h + data).write(to: URL(fileURLWithPath: out))
    print("wrote \(out): \(String(format: "%.4f", Double(data.count / 8) / 48000)) s")
}

/// QC: grabs decoded frames from a finished movie (not from the edit list) into one PNG per time.
func grab(_ path: String, _ outPrefix: String, _ times: [Double]) {
    let gen = AVAssetImageGenerator(asset: AVURLAsset(url: URL(fileURLWithPath: path)))
    gen.requestedTimeToleranceBefore = .zero; gen.requestedTimeToleranceAfter = .zero
    for t in times {
        guard let cg = try? gen.copyCGImage(at: CMTime(seconds: t, preferredTimescale: 24000), actualTime: nil) else { fail("no frame at \(t)") }
        let file = outPrefix + String(format: "%06.2f", t) + ".png"
        let dst = CGImageDestinationCreateWithURL(URL(fileURLWithPath: file) as CFURL, "public.png" as CFString, 1, nil)!
        CGImageDestinationAddImage(dst, cg, nil)
        CGImageDestinationFinalize(dst)
    }
    print("grabbed \(times.count) frames")
}

let a = CommandLine.arguments
guard a.count >= 3 else { fail("usage: cut render|still|mux|probe|pcm|grab ...") }
switch a[1] {
case "render": render(a[2], a[3], from: a.count > 5 ? Double(a[4]) : nil, to: a.count > 5 ? Double(a[5]) : nil)
case "still": still(a[2], Double(a[3])!, a[4])
case "mux": mux(a[2], a[3], a[4])
case "probe": probe(a[2])
case "pcm": pcm(a[2], a[3])
case "grab": grab(a[2], a[3], a[4...].compactMap { Double($0) })
default: fail("unknown command \(a[1])")
}
