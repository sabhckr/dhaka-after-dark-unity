// World-reveal trailer renderer (2026-11-07). Cuts straight from the source recording; macOS frameworks only.
//
//   reveal render <edl.json> <out.mov> [fromSecond toSecond]   picture only, H.264 from the edit list
//   reveal still <edl.json> <out.png> <second ...>             timeline frames, for review
//   reveal mux <video.mov> <audio.wav> <out.mp4>               passthrough picture + AAC sound
//   reveal probe <movie>                                       duration, size, frame rate, audio
//   reveal pcm <movie> <out.wav>                               decoded sound as float WAV (to meter the delivery)
//   reveal grab <movie> <outPrefix> <second ...>               decoded frames from a finished movie
//
// The edit list holds video clips (source in-point, timeline start, duration, speed, fades, a modest push, per-clip
// exposure/temperature/saturation) and texts (style, animation, timing). Nothing is drawn into the picture except
// typography, thin rules, grain, vignette and the letterbox. Build:
//   /Library/Developer/CommandLineTools/usr/bin/swiftc -O -swift-version 5 \
//     -sdk /Library/Developer/CommandLineTools/SDKs/MacOSX26.sdk -target arm64-apple-macosx14.0 reveal.swift -o reveal
import AVFoundation
import AppKit
import CoreImage
import CoreText
import Foundation

func fail(_ m: String) -> Never { FileHandle.standardError.write((m + "\n").data(using: .utf8)!); exit(1) }
let srgb = CGColorSpace(name: CGColorSpace.sRGB)!

struct Clip: Decodable {
    var id: String; var t: Double; var d: Double; var `in`: Double
    var speed: Double?; var still: Bool?; var fin: Double?; var fout: Double?
    var zoom: [Double]?; var centre: [Double]?
    var exposure: Double?; var temp: Double?; var sat: Double?; var contrast: Double?
}
struct TextItem: Decodable {
    var style: String; var text: String; var t: Double; var d: Double
    var anim: String?; var y: Double?; var rule: Bool?; var accent: Bool?; var dim: Double?; var size: Double?; var gap: Double?
}
struct Grade: Decodable { var saturation: Double?; var contrast: Double?; var cool: Double?; var grain: Double?; var vignette: Double?; var lift: Double? }
struct EDL: Decodable {
    var source: String; var width: Int; var height: Int; var fps: Int; var duration: Double; var bars: Int
    var grade: Grade?; var clips: [Clip]; var texts: [TextItem]
}

// MARK: source decoding (one sequential reader per clip; the timeline is rendered in order)
final class ClipReader {
    let clip: Clip, asset: AVURLAsset
    var reader: AVAssetReader?, output: AVAssetReaderTrackOutput?
    var current: CIImage?, pending: (CIImage, Double)?, ended = false
    init(_ c: Clip, _ a: AVURLAsset) { clip = c; asset = a }
    func start() {
        guard let vt = asset.tracks(withMediaType: .video).first else { fail("no video track") }
        let r = try! AVAssetReader(asset: asset)
        let o = AVAssetReaderTrackOutput(track: vt, outputSettings: [kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA])
        o.alwaysCopiesSampleData = false
        let span = clip.still == true ? 0.4 : clip.d * (clip.speed ?? 1) + 0.25
        r.timeRange = CMTimeRange(start: CMTime(seconds: clip.in, preferredTimescale: 600), duration: CMTime(seconds: span, preferredTimescale: 600))
        r.add(o); guard r.startReading() else { fail("reader \(clip.id): \(String(describing: r.error))") }
        reader = r; output = o
    }
    func pull() -> (CIImage, Double)? {
        guard let sb = output!.copyNextSampleBuffer(), let pb = CMSampleBufferGetImageBuffer(sb) else { ended = true; return nil }
        return (CIImage(cvPixelBuffer: pb), CMTimeGetSeconds(CMSampleBufferGetPresentationTimeStamp(sb)))
    }
    /// The decoded frame for source time `s`: the last sample at or before it (frames are held when the clip runs slow).
    func frame(at s: Double) -> CIImage {
        if reader == nil { start() }
        if current == nil { if let p = pull() { current = p.0 } else { fail("clip \(clip.id): no frames at \(clip.in)") } }
        if clip.still == true { return current! }
        while !ended {
            if pending == nil { pending = pull(); if pending == nil { break } }
            if pending!.1 <= s + 0.5 / 30 { current = pending!.0; pending = nil } else { break }
        }
        return current!
    }
    func close() { reader?.cancelReading(); reader = nil; output = nil; current = nil; pending = nil }
}

// MARK: typography
let nearWhite = CGColor(red: 0.93, green: 0.94, blue: 0.95, alpha: 1)
let silver = CGColor(red: 0.765, green: 0.784, blue: 0.808, alpha: 1)      // #C3C8CE
let steel = CGColor(red: 0.518, green: 0.565, blue: 0.612, alpha: 1)       // #84909C
let crimson = CGColor(red: 0.553, green: 0.094, blue: 0.141, alpha: 1)     // #8D1824

func font(_ name: String, _ size: CGFloat) -> CTFont {
    let f = CTFontCreateWithName(name as CFString, size, nil)
    let got = CTFontCopyPostScriptName(f) as String
    if got != name { fail("font \(name) is not installed (got \(got))") }
    return f
}

final class Typesetter {
    let w: Int, h: Int
    init(w: Int, h: Int) { self.w = w; self.h = h }

    struct Spec { var font: String; var size: CGFloat; var kern: CGFloat; var color: CGColor; var gap: CGFloat; var shadow: CGFloat }
    func spec(_ style: String, _ m: CGFloat) -> Spec {
        switch style {
        case "presents": return Spec(font: "AvenirNext-Medium", size: 30 * m, kern: 12 * m, color: silver, gap: 40 * m, shadow: 0)
        case "statement": return Spec(font: "AvenirNextCondensed-DemiBold", size: 84 * m, kern: 7 * m, color: nearWhite, gap: 96 * m, shadow: 22)
        case "identity": return Spec(font: "AvenirNextCondensed-Heavy", size: 112 * m, kern: 9 * m, color: nearWhite, gap: 124 * m, shadow: 26)
        case "title": return Spec(font: "AvenirNextCondensed-Heavy", size: 150 * m, kern: 13 * m, color: nearWhite, gap: 165 * m, shadow: 30)
        case "tagline": return Spec(font: "AvenirNext-Medium", size: 31 * m, kern: 13 * m, color: silver, gap: 44 * m, shadow: 12)
        case "credit": return Spec(font: "AvenirNext-Medium", size: 32 * m, kern: 10 * m, color: silver, gap: 44 * m, shadow: 0)
        case "studio": return Spec(font: "AvenirNextCondensed-Heavy", size: 66 * m, kern: 26 * m, color: nearWhite, gap: 80 * m, shadow: 0)
        case "date": return Spec(font: "AvenirNext-DemiBold", size: 33 * m, kern: 10 * m, color: nearWhite, gap: 44 * m, shadow: 0)
        default: fail("unknown text style \(style)")
        }
    }

    /// Centred lines; `y` is the first baseline from the bottom of the frame. A thin rule sits under the block.
    func image(_ t: TextItem) -> CIImage {
        let s = spec(t.style, CGFloat(t.size ?? 1))
        let f = font(s.font, s.size)
        let lines = t.text.components(separatedBy: "\n")
        func line(_ str: String) -> CTLine {
            CTLineCreateWithAttributedString(NSAttributedString(string: str, attributes: [.font: f, .foregroundColor: s.color, .kern: s.kern]))
        }
        let widest = lines.map { CGFloat(CTLineGetTypographicBounds(line($0), nil, nil, nil)) - s.kern }.max() ?? 0
        if widest > CGFloat(w) * 0.92 { fail("text too wide: \(t.text)") }
        let ctx = CGContext(data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4, space: srgb, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        let y0 = CGFloat(t.y ?? 0.5) * CGFloat(h)
        let gap = CGFloat(t.gap ?? 1) * s.gap
        // The block is centred on y0: baseline of the first line sits above it.
        let blockH = gap * CGFloat(lines.count - 1)
        let firstBaseline = y0 + blockH / 2 - s.size * 0.36
        if s.shadow > 0 { ctx.setShadow(offset: .zero, blur: s.shadow, color: CGColor(red: 0, green: 0, blue: 0, alpha: 0.8)) }
        for (i, str) in lines.enumerated() {
            let l = line(str)
            let width = CGFloat(CTLineGetTypographicBounds(l, nil, nil, nil)) - s.kern   // the trailing kern is not visible
            ctx.textPosition = CGPoint(x: (CGFloat(w) - width) / 2, y: firstBaseline - CGFloat(i) * gap)
            CTLineDraw(l, ctx)
        }
        if t.rule == true {
            ctx.setShadow(offset: .zero, blur: 0, color: nil)
            let ry = firstBaseline - blockH - s.size * 0.42
            let rw: CGFloat = t.style == "title" ? 260 : 150
            ctx.setFillColor(t.accent == true ? crimson : CGColor(red: 0.518, green: 0.565, blue: 0.612, alpha: 0.75))
            ctx.fill(CGRect(x: (CGFloat(w) - rw) / 2, y: ry, width: rw, height: t.style == "title" ? 3 : 2))
        }
        return CIImage(cgImage: ctx.makeImage()!)
    }
}

// MARK: the frame
func hashf(_ i: Int, _ j: Int) -> Double {
    var x = UInt64(truncatingIfNeeded: i) &* 0x9E3779B97F4A7C15 ^ UInt64(truncatingIfNeeded: j) &* 0xC2B2AE3D27D4EB4F
    x ^= x >> 31; x = x &* 0xBF58476D1CE4E5B9; x ^= x >> 29
    return Double(x % 1_000_003) / 1_000_003
}
func ease(_ u: Double) -> Double { let x = max(0, min(1, u)); return x * x * (3 - 2 * x) }

final class Renderer {
    let e: EDL, W: CGFloat, H: CGFloat, black: CIImage, asset: AVURLAsset
    var readers: [Int: ClipReader] = [:]
    let texts: [CIImage]
    let grainSource = CIFilter(name: "CIRandomGenerator")!.outputImage!

    init(_ e: EDL) {
        self.e = e; W = CGFloat(e.width); H = CGFloat(e.height)
        asset = AVURLAsset(url: URL(fileURLWithPath: e.source))
        black = CIImage(color: CIColor(red: 0, green: 0, blue: 0)).cropped(to: CGRect(x: 0, y: 0, width: e.width, height: e.height))
        let ts = Typesetter(w: e.width, h: e.height)
        texts = e.texts.map { ts.image($0) }
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
    func look(_ img: CIImage, _ c: Clip) -> CIImage {
        var o = img
        if let ev = c.exposure, ev != 0 { o = o.applyingFilter("CIExposureAdjust", parameters: ["inputEV": ev]) }
        if let k = c.temp, k != 0 { o = o.applyingFilter("CITemperatureAndTint", parameters: ["inputNeutral": CIVector(x: 6500, y: 0), "inputTargetNeutral": CIVector(x: 6500 + CGFloat(k), y: 0)]) }
        if c.sat != nil || c.contrast != nil { o = o.applyingFilter("CIColorControls", parameters: ["inputSaturation": c.sat ?? 1, "inputContrast": c.contrast ?? 1, "inputBrightness": 0]) }
        return o
    }

    func frame(_ t: Double, index: Int) -> CIImage {
        var out = black
        for (ci, c) in e.clips.enumerated() {
            if t >= c.t + c.d + 0.5, let r = readers[ci] { r.close(); readers[ci] = nil }
            let a = alpha(c, t)
            if a <= 0 { continue }
            let r = readers[ci] ?? { let r = ClipReader(c, asset); readers[ci] = r; return r }()
            let u = max(0, min(1, (t - c.t) / c.d))
            let src = c.in + (t - c.t) * (c.speed ?? 1)
            var img = look(r.frame(at: src), c)
            let z0 = c.zoom?[0] ?? 1, z1 = c.zoom?[1] ?? z0
            let z = z0 + (z1 - z0) * u
            if abs(z - 1) > 1e-4 || c.centre != nil {
                let cx = CGFloat(c.centre?[0] ?? 0.5) * W, cy = CGFloat(1 - (c.centre?[1] ?? 0.5)) * H
                let hx = W / (2 * CGFloat(z)), hy = H / (2 * CGFloat(z))
                let px = min(max(cx, hx), W - hx), py = min(max(cy, hy), H - hy)
                img = img.transformed(by: CGAffineTransform(translationX: -px, y: -py).concatenating(CGAffineTransform(scaleX: CGFloat(z), y: CGFloat(z))).concatenating(CGAffineTransform(translationX: W / 2, y: H / 2)))
            }
            out = scale(img.cropped(to: black.extent), 1, alpha: a).composited(over: out)
        }
        // Global grade: gentle S curve, a touch less saturation, a cool cast, vignette, fine grain.
        let g = e.grade
        out = out.applyingFilter("CIColorControls", parameters: ["inputSaturation": g?.saturation ?? 0.86, "inputContrast": g?.contrast ?? 1.0, "inputBrightness": 0])
        let lift = CGFloat(g?.lift ?? 0)
        out = out.applyingFilter("CIToneCurve", parameters: [
            "inputPoint0": CIVector(x: 0, y: lift), "inputPoint1": CIVector(x: 0.25, y: 0.225), "inputPoint2": CIVector(x: 0.5, y: 0.5),
            "inputPoint3": CIVector(x: 0.75, y: 0.775), "inputPoint4": CIVector(x: 1, y: 1)])
        let cool = CGFloat(g?.cool ?? 1)
        out = out.applyingFilter("CIColorMatrix", parameters: [
            "inputRVector": CIVector(x: 1 - 0.025 * cool, y: 0, z: 0, w: 0), "inputGVector": CIVector(x: 0, y: 1 - 0.005 * cool, z: 0, w: 0),
            "inputBVector": CIVector(x: 0, y: 0, z: 1 + 0.03 * cool, w: 0), "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 1)])
        // Text dimming: the picture darkens a little under statements so type reads over bright shots.
        var dim = 0.0
        for tx in e.texts where t >= tx.t - 0.3 && t <= tx.t + tx.d + 0.3 {
            let d = tx.dim ?? 0
            if d > 0 { dim = max(dim, d * min(1, min((t - tx.t + 0.3) / 0.3, (tx.t + tx.d + 0.3 - t) / 0.3))) }
        }
        if dim > 0 { out = scale(out, 1 - dim) }
        let gx = CGFloat(hashf(index, 1) * 4000), gy = CGFloat(hashf(index, 2) * 4000)
        let noise = grainSource.transformed(by: CGAffineTransform(translationX: -gx, y: -gy)).cropped(to: black.extent)
        let ga = CGFloat(g?.grain ?? 0.12)
        let grain = noise.applyingFilter("CIColorMatrix", parameters: [
            "inputRVector": CIVector(x: ga, y: 0, z: 0, w: 0), "inputGVector": CIVector(x: ga, y: 0, z: 0, w: 0),
            "inputBVector": CIVector(x: ga, y: 0, z: 0, w: 0), "inputAVector": CIVector(x: 0, y: 0, z: 0, w: 0),
            "inputBiasVector": CIVector(x: 1 - ga / 2, y: 1 - ga / 2, z: 1 - ga / 2, w: 1)])
        out = grain.applyingFilter("CIMultiplyCompositing", parameters: ["inputBackgroundImage": out]).cropped(to: black.extent)
        out = out.applyingFilter("CIVignetteEffect", parameters: ["inputCenter": CIVector(x: W / 2, y: H / 2), "inputRadius": max(W, H) * 0.5, "inputIntensity": g?.vignette ?? 0.35, "inputFalloff": 0.6])
        let bar = CGFloat(e.bars)
        if bar > 0 {
            let bars = CIImage(color: CIColor(red: 0, green: 0, blue: 0)).cropped(to: CGRect(x: 0, y: 0, width: W, height: bar))
                .composited(over: CIImage(color: CIColor(red: 0, green: 0, blue: 0)).cropped(to: CGRect(x: 0, y: H - bar, width: W, height: bar)))
            out = bars.composited(over: out)
        }
        for (i, tx) in e.texts.enumerated() where t >= tx.t && t <= tx.t + tx.d {
            let x = t - tx.t, rem = tx.t + tx.d - t
            var a = 1.0, s = 1.0, dy = 0.0
            switch tx.anim ?? "editorial" {
            case "editorial": a = min(ease(x / 0.7), ease(rem / 0.7))
            case "reveal": a = min(ease(x / 0.4), ease(rem / 0.5)); dy = -8 * (1 - ease(x / 0.4))
            case "impact": a = min(min(1, x / 0.07), ease(rem / 0.35)); s = 1 + 0.035 * (1 - ease(x / 0.14))
            case "title": a = min(min(1, x / 0.1), ease(rem / 0.6)); s = 1 + 0.03 * x / tx.d
            case "card": a = min(ease(x / 0.7), ease(rem / 0.9))
            default: fail("unknown animation \(tx.anim!)")
            }
            var img = texts[i]
            if s != 1 || dy != 0 {
                img = img.transformed(by: CGAffineTransform(translationX: -W / 2, y: -H / 2).concatenating(CGAffineTransform(scaleX: CGFloat(s), y: CGFloat(s))).concatenating(CGAffineTransform(translationX: W / 2, y: H / 2 + CGFloat(dy))))
            }
            out = scale(img, 1, alpha: a).composited(over: out)
        }
        return out.cropped(to: black.extent)
    }
}

func loadEDL(_ path: String) -> EDL {
    do { return try JSONDecoder().decode(EDL.self, from: Data(contentsOf: URL(fileURLWithPath: path))) } catch { fail("edit list: \(error)") }
}
func savePNG(_ cg: CGImage, _ path: String) {
    guard let dst = CGImageDestinationCreateWithURL(URL(fileURLWithPath: path) as CFURL, "public.png" as CFString, 1, nil) else { fail("cannot write \(path)") }
    CGImageDestinationAddImage(dst, cg, nil); CGImageDestinationFinalize(dst)
}

// MARK: commands
func render(_ edlPath: String, _ outPath: String, from: Double?, to: Double?) {
    let e = loadEDL(edlPath), r = Renderer(e)
    let url = URL(fileURLWithPath: outPath); try? FileManager.default.removeItem(at: url)
    let writer = try! AVAssetWriter(outputURL: url, fileType: .mov)
    let settings: [String: Any] = [
        AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: e.width, AVVideoHeightKey: e.height,
        AVVideoColorPropertiesKey: [AVVideoColorPrimariesKey: AVVideoColorPrimaries_ITU_R_709_2, AVVideoTransferFunctionKey: AVVideoTransferFunction_ITU_R_709_2, AVVideoYCbCrMatrixKey: AVVideoYCbCrMatrix_ITU_R_709_2],
        AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 24_000_000, AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
                                          AVVideoExpectedSourceFrameRateKey: e.fps, AVVideoMaxKeyFrameIntervalKey: e.fps]]
    let input = AVAssetWriterInput(mediaType: .video, outputSettings: settings); input.expectsMediaDataInRealTime = false
    let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [
        kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA, kCVPixelBufferWidthKey as String: e.width, kCVPixelBufferHeightKey as String: e.height])
    writer.add(input); writer.startWriting(); writer.startSession(atSourceTime: .zero)
    let ctx = CIContext(options: [.outputColorSpace: srgb, .cacheIntermediates: false])
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
        if f % 300 == 0 { print("frame \(f) of \(f1), \(String(format: "%.0f", Date().timeIntervalSince(start))) s") }
    }
    input.markAsFinished()
    let sem = DispatchSemaphore(value: 0); writer.finishWriting { sem.signal() }; sem.wait()
    if writer.status != .completed { fail("writer: \(String(describing: writer.error))") }
    print("wrote \(f1 - f0) frames to \(outPath) in \(String(format: "%.0f", Date().timeIntervalSince(start))) s")
}

/// Timeline stills: the frames are rendered in order, so every clip before each time is decoded sequentially as in a render.
func stills(_ edlPath: String, _ outPrefix: String, _ times: [Double]) {
    let e = loadEDL(edlPath), r = Renderer(e)
    let ctx = CIContext(options: [.outputColorSpace: srgb])
    let fps = Double(e.fps)
    var f = 0
    for t in times.sorted() {
        let target = Int((t * fps).rounded())
        while f < target { autoreleasepool { _ = r.frame(Double(f) / fps, index: f) }; f += 1 }   // advance the readers (cheap: no rendering)
        let img = r.frame(Double(f) / fps, index: f)
        let cg = ctx.createCGImage(img, from: img.extent, format: .RGBA8, colorSpace: srgb)!
        savePNG(cg, String(format: "%@_%06.2f.png", outPrefix, t))
    }
}

func mux(_ video: String, _ audio: String, _ out: String) {
    let va = AVURLAsset(url: URL(fileURLWithPath: video)), aa = AVURLAsset(url: URL(fileURLWithPath: audio))
    guard let vt = va.tracks(withMediaType: .video).first, let at = aa.tracks(withMediaType: .audio).first else { fail("no tracks") }
    let url = URL(fileURLWithPath: out); try? FileManager.default.removeItem(at: url)
    let vr = try! AVAssetReader(asset: va), vo = AVAssetReaderTrackOutput(track: vt, outputSettings: nil); vr.add(vo)
    let ar = try! AVAssetReader(asset: aa)
    let ao = AVAssetReaderTrackOutput(track: at, outputSettings: [AVFormatIDKey: kAudioFormatLinearPCM, AVLinearPCMBitDepthKey: 32, AVLinearPCMIsFloatKey: true, AVLinearPCMIsNonInterleaved: false, AVSampleRateKey: 48000, AVNumberOfChannelsKey: 2]); ar.add(ao)
    let w = try! AVAssetWriter(outputURL: url, fileType: .mp4)
    let vi = AVAssetWriterInput(mediaType: .video, outputSettings: nil, sourceFormatHint: (vt.formatDescriptions.first as! CMFormatDescription)); vi.expectsMediaDataInRealTime = false
    var acl = AudioChannelLayout(); acl.mChannelLayoutTag = kAudioChannelLayoutTag_Stereo
    let ai = AVAssetWriterInput(mediaType: .audio, outputSettings: [AVFormatIDKey: kAudioFormatMPEG4AAC, AVSampleRateKey: 48000, AVNumberOfChannelsKey: 2, AVEncoderBitRateKey: 320_000,
        AVChannelLayoutKey: Data(bytes: &acl, count: MemoryLayout<AudioChannelLayout>.size)]); ai.expectsMediaDataInRealTime = false
    w.add(vi); w.add(ai); w.shouldOptimizeForNetworkUse = true
    guard w.startWriting() else { fail("writer: \(String(describing: w.error))") }
    w.startSession(atSourceTime: .zero); vr.startReading(); ar.startReading()
    // Each input pulls from its reader when the writer asks for data, which lets the writer interleave the tracks.
    var n = 0, m = 0
    let group = DispatchGroup()
    func pump(_ input: AVAssetWriterInput, _ out: AVAssetReaderTrackOutput, _ label: String, _ count: UnsafeMutablePointer<Int>) {
        group.enter()
        input.requestMediaDataWhenReady(on: DispatchQueue(label: label)) {
            while input.isReadyForMoreMediaData {
                guard let sb = out.copyNextSampleBuffer() else { input.markAsFinished(); group.leave(); return }
                if !input.append(sb) { fail("\(label) append: \(String(describing: w.error))") }
                count.pointee += 1
            }
        }
    }
    pump(vi, vo, "video", &n); pump(ai, ao, "audio", &m)
    group.wait()
    let sem = DispatchSemaphore(value: 0); w.finishWriting { sem.signal() }; sem.wait()
    if w.status != .completed { fail("mux: \(String(describing: w.error))") }
    print("muxed \(n) video samples and \(m) audio buffers into \(out)")
}

func probe(_ path: String) {
    let a = AVURLAsset(url: URL(fileURLWithPath: path)); let d = CMTimeGetSeconds(a.duration)
    print("duration \(String(format: "%.3f", d)) s")
    for t in a.tracks {
        if t.mediaType == .video { print("video \(Int(t.naturalSize.width))x\(Int(t.naturalSize.height)) \(t.nominalFrameRate) fps \(Int(t.estimatedDataRate / 1000)) kbit/s, \(String(format: "%.3f", CMTimeGetSeconds(t.timeRange.duration))) s") }
        if t.mediaType == .audio, let fd = t.formatDescriptions.first {
            let asbd = CMAudioFormatDescriptionGetStreamBasicDescription(fd as! CMFormatDescription)!.pointee
            print("audio \(asbd.mFormatID == kAudioFormatMPEG4AAC ? "AAC" : "\(asbd.mFormatID)") \(Int(asbd.mSampleRate)) Hz \(asbd.mChannelsPerFrame) ch \(Int(t.estimatedDataRate / 1000)) kbit/s, \(String(format: "%.3f", CMTimeGetSeconds(t.timeRange.duration))) s")
        }
    }
}

func pcm(_ path: String, _ out: String) {
    let a = AVURLAsset(url: URL(fileURLWithPath: path))
    guard let at = a.tracks(withMediaType: .audio).first else { fail("no audio") }
    let r = try! AVAssetReader(asset: a)
    let o = AVAssetReaderTrackOutput(track: at, outputSettings: [AVFormatIDKey: kAudioFormatLinearPCM, AVLinearPCMBitDepthKey: 32, AVLinearPCMIsFloatKey: true, AVLinearPCMIsNonInterleaved: false, AVSampleRateKey: 48000, AVNumberOfChannelsKey: 2]); r.add(o); r.startReading()
    var data = Data()
    while let sb = o.copyNextSampleBuffer() {
        guard let bb = CMSampleBufferGetDataBuffer(sb) else { continue }
        var len = 0; var p: UnsafeMutablePointer<Int8>? = nil
        CMBlockBufferGetDataPointer(bb, atOffset: 0, lengthAtOffsetOut: nil, totalLengthOut: &len, dataPointerOut: &p)
        data.append(UnsafeBufferPointer(start: UnsafeRawPointer(p!).assumingMemoryBound(to: UInt8.self), count: len))
    }
    var h = Data(); func u32(_ v: UInt32) { var x = v.littleEndian; h.append(Data(bytes: &x, count: 4)) }; func u16(_ v: UInt16) { var x = v.littleEndian; h.append(Data(bytes: &x, count: 2)) }
    h.append("RIFF".data(using: .ascii)!); u32(UInt32(36 + data.count)); h.append("WAVEfmt ".data(using: .ascii)!); u32(16); u16(3); u16(2); u32(48000); u32(48000 * 8); u16(8); u16(32); h.append("data".data(using: .ascii)!); u32(UInt32(data.count))
    try! (h + data).write(to: URL(fileURLWithPath: out))
    print("wrote \(data.count / 8) frames (\(String(format: "%.3f", Double(data.count / 8) / 48000)) s) to \(out)")
}

func grab(_ path: String, _ prefix: String, _ times: [Double]) {
    let a = AVURLAsset(url: URL(fileURLWithPath: path)); let g = AVAssetImageGenerator(asset: a)
    g.requestedTimeToleranceBefore = .zero; g.requestedTimeToleranceAfter = .zero
    for t in times { let img = try! g.copyCGImage(at: CMTime(seconds: t, preferredTimescale: 600), actualTime: nil); savePNG(img, String(format: "%@_%06.2f.png", prefix, t)) }
}

let a = CommandLine.arguments
guard a.count >= 3 else { fail("usage: reveal render|still|mux|probe|pcm|grab ...") }
switch a[1] {
case "render": render(a[2], a[3], from: a.count > 5 ? Double(a[4]) : nil, to: a.count > 5 ? Double(a[5]) : nil)
case "still": stills(a[2], a[3], a[4...].compactMap { Double($0) })
case "mux": mux(a[2], a[3], a[4])
case "probe": probe(a[2])
case "pcm": pcm(a[2], a[3])
case "grab": grab(a[2], a[3], a[4...].compactMap { Double($0) })
default: fail("unknown command \(a[1])")
}
