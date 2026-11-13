// Puts the 90-second cinematic together: the frames Unity rendered, the English subtitles, the title and the sound, into one H.264/AAC file of exactly 2700 frames.
//
//   swift cinematic_assemble.swift <framesDir> <audio.wav> <subtitles.json> <out.mp4> [everyNth]
//
// A missing frame is black. everyNth > 1 makes a quick preview (held frames, same timing).
import AVFoundation
import AppKit
import CoreText
import Foundation

struct Sub: Decodable { let start: Double; let end: Double; let who: String; let text: String }
let args = CommandLine.arguments
guard args.count >= 5 else { print("usage: <framesDir> <audio.wav> <subtitles.json> <out.mp4> [everyNth]"); exit(2) }
let frames = args[1], audio = args[2], out = args[4], nth = args.count > 5 ? Int(args[5])! : 1
let subs = try JSONDecoder().decode([Sub].self, from: Data(contentsOf: URL(fileURLWithPath: args[3])))
let W = 1920, H = 1080, FPS: Int32 = 30, N = 2700
let titleStart = 88.0, labelEnd = 2.0
try? FileManager.default.removeItem(atPath: out)

func font(_ name: String, _ size: CGFloat) -> NSFont { NSFont(name: name, size: size) ?? NSFont.systemFont(ofSize: size) }
func color(_ r: CGFloat, _ g: CGFloat, _ b: CGFloat, _ a: CGFloat = 1) -> CGColor { CGColor(srgbRed: r, green: g, blue: b, alpha: a) }
let silver = color(0.765, 0.784, 0.808), steel = color(0.518, 0.565, 0.612), crimson = color(0.553, 0.094, 0.141)

func line(_ s: String, _ f: NSFont, _ c: CGColor, kern: CGFloat = 0) -> CTLine {
    CTLineCreateWithAttributedString(NSAttributedString(string: s, attributes: [.font: f, .foregroundColor: c, .kern: kern]))
}
func width(_ l: CTLine) -> CGFloat { CGFloat(CTLineGetTypographicBounds(l, nil, nil, nil)) }
func draw(_ c: CGContext, _ l: CTLine, x: CGFloat, y: CGFloat) { c.textPosition = CGPoint(x: x, y: y); CTLineDraw(l, c) }
func centred(_ c: CGContext, _ l: CTLine, y: CGFloat) { draw(c, l, x: (CGFloat(W) - width(l)) / 2, y: y) }

func wrap(_ text: String, _ f: NSFont, maxW: CGFloat) -> [String] {
    var lines: [String] = [], cur = ""
    for word in text.split(separator: " ") {
        let t = cur.isEmpty ? String(word) : cur + " " + word
        if width(line(t, f, silver)) > maxW, !cur.isEmpty { lines.append(cur); cur = String(word) } else { cur = t }
    }
    if !cur.isEmpty { lines.append(cur) }
    return lines
}

let subFont = font("AvenirNext-Medium", 40), nameFont = font("AvenirNext-DemiBold", 24)
func subtitle(_ c: CGContext, _ s: Sub, _ t: Double) {
    let a = CGFloat(min(1, min((t - s.start) / 0.13, (s.end - t) / 0.13)))
    guard a > 0 else { return }
    c.saveGState()
    c.setAlpha(a)
    c.setShadow(offset: CGSize(width: 0, height: -2), blur: 8, color: color(0, 0, 0, 0.9))
    let lines = wrap(s.text, subFont, maxW: 1320)
    var y = CGFloat(88) + CGFloat(lines.count - 1) * 52
    let name = line(s.who, nameFont, steel, kern: 3)
    centred(c, name, y: y + 54)
    for l in lines { centred(c, line(l, subFont, .white), y: y); y -= 52 }
    c.restoreGState()
}

func title(_ c: CGContext, _ t: Double) {
    let u = t - titleStart
    guard u >= 0 else { return }
    c.setFillColor(color(0.02, 0.024, 0.031))
    c.fill(CGRect(x: 0, y: 0, width: W, height: H))
    let a = CGFloat(min(1, u / 0.22))
    c.saveGState()
    c.setAlpha(a)
    let big = font("AvenirNext-Heavy", 128)
    centred(c, line("DHAKA: AFTER DARK", big, .white, kern: 12), y: 500)
    c.setFillColor(crimson)
    c.fill(CGRect(x: CGFloat(W) / 2 - 90, y: 468, width: 180, height: 3))
    c.setAlpha(a * CGFloat(min(1, max(0, (u - 0.25) / 0.25))))
    centred(c, line("A CITY THAT NEVER SLEEPS.", font("AvenirNext-Medium", 30), silver, kern: 9), y: 410)
    centred(c, line("IN-ENGINE CINEMATIC. NOT GAMEPLAY.", font("AvenirNext-Medium", 18), steel, kern: 4), y: 66)
    c.restoreGState()
}

func label(_ c: CGContext, _ t: Double) {
    guard t < labelEnd else { return }
    let a = CGFloat(min(1, min(t / 0.3, (labelEnd - t) / 0.3)))
    c.saveGState(); c.setAlpha(a)
    centred(c, line("IN-ENGINE CINEMATIC. NOT GAMEPLAY.", font("AvenirNext-Medium", 20), steel, kern: 4), y: 530)
    c.restoreGState()
}

let writer = try AVAssetWriter(outputURL: URL(fileURLWithPath: out), fileType: .mp4)
let vInput = AVAssetWriterInput(mediaType: .video, outputSettings: [
    AVVideoCodecKey: AVVideoCodecType.h264, AVVideoWidthKey: W, AVVideoHeightKey: H,
    AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 22_000_000, AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel, AVVideoExpectedSourceFrameRateKey: 30, AVVideoMaxKeyFrameIntervalKey: 30]
])
vInput.expectsMediaDataInRealTime = false
let adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: vInput, sourcePixelBufferAttributes: [
    kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA, kCVPixelBufferWidthKey as String: W, kCVPixelBufferHeightKey as String: H])
writer.add(vInput)
let aInput = AVAssetWriterInput(mediaType: .audio, outputSettings: [AVFormatIDKey: kAudioFormatMPEG4AAC, AVSampleRateKey: 48000, AVNumberOfChannelsKey: 2, AVEncoderBitRateKey: 320_000])
aInput.expectsMediaDataInRealTime = false
writer.add(aInput)
writer.startWriting()
writer.startSession(atSourceTime: .zero)

func image(_ i: Int) -> CGImage? {
    let f = (i / nth) * nth
    let url = URL(fileURLWithPath: String(format: "%@/f_%05d.jpg", frames, f)) as CFURL
    guard let src = CGImageSourceCreateWithURL(url, nil) else { return nil }
    return CGImageSourceCreateImageAtIndex(src, 0, nil)
}

let q = DispatchQueue(label: "encode")
let done = DispatchGroup()
done.enter()
var i = 0
vInput.requestMediaDataWhenReady(on: q) {
    while vInput.isReadyForMoreMediaData {
        if i >= N { vInput.markAsFinished(); done.leave(); return }
        autoreleasepool {
            var pb: CVPixelBuffer?
            CVPixelBufferPoolCreatePixelBuffer(nil, adaptor.pixelBufferPool!, &pb)
            CVPixelBufferLockBaseAddress(pb!, [])
            let cs = CGColorSpace(name: CGColorSpace.sRGB)!
            let c = CGContext(data: CVPixelBufferGetBaseAddress(pb!), width: W, height: H, bitsPerComponent: 8, bytesPerRow: CVPixelBufferGetBytesPerRow(pb!), space: cs, bitmapInfo: CGImageAlphaInfo.premultipliedFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue)!
            c.setFillColor(color(0, 0, 0)); c.fill(CGRect(x: 0, y: 0, width: W, height: H))
            if let img = image(i) { c.draw(img, in: CGRect(x: 0, y: 0, width: W, height: H)) }
            let t = Double(i) / 30
            for s in subs where t >= s.start && t < s.end { subtitle(c, s, t) }
            label(c, t)
            title(c, t)
            CVPixelBufferUnlockBaseAddress(pb!, [])
            adaptor.append(pb!, withPresentationTime: CMTime(value: CMTimeValue(i), timescale: FPS))
        }
        i += 1
        if i % 300 == 0 { print("frame \(i)/\(N)") }
    }
}
let asset = AVURLAsset(url: URL(fileURLWithPath: audio))
let track = try await asset.loadTracks(withMediaType: .audio)[0]
let reader = try AVAssetReader(asset: asset)
let output = AVAssetReaderTrackOutput(track: track, outputSettings: [AVFormatIDKey: kAudioFormatLinearPCM, AVLinearPCMBitDepthKey: 16, AVLinearPCMIsFloatKey: false, AVLinearPCMIsBigEndianKey: false, AVLinearPCMIsNonInterleaved: false, AVSampleRateKey: 48000, AVNumberOfChannelsKey: 2])
reader.add(output)
reader.startReading()
let qa = DispatchQueue(label: "audio")
done.enter()
aInput.requestMediaDataWhenReady(on: qa) {
    while aInput.isReadyForMoreMediaData {
        if let b = output.copyNextSampleBuffer() { aInput.append(b) } else { aInput.markAsFinished(); done.leave(); return }
    }
}
done.wait()
await writer.finishWriting()
print(writer.status == .completed ? "wrote \(out), \(N) frames" : "FAILED \(String(describing: writer.error))")
