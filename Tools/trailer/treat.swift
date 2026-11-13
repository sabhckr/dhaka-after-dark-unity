// Trailer still treatment (trailer v1, 2026-11-03): turns a game render into a graded night frame.
// macOS frameworks only. Sources are read, never written.
//
//   treat <recipes.json> <outDir> [name ...]
//
// Each recipe names a source render and a mode:
//   day    a cinematic day grade only; nothing added (genuine).
//   night  day-for-night: the render darkened and cooled, its sky replaced by a night sky.
//   keep   a render already at dusk or night, kept, with additions on top.
// Artistic additions, all optional and all marked as additions in the trailer (they are NOT in the game):
//   windows  a share of the render's own window panes lit (found as dark panes in pale walls);
//   signs    the render's own colour signboards made to glow (found by saturation above the horizon);
//   wet      wet-road reflections, the frame mirrored about the camera's true horizon (from the view's pose)
//            over the road surface (flood-filled from the bottom edge);
//   lamps    glows at given points (source pixels), with an optional light pool on the ground below.
// Output: <outDir>/<name>.png at 1920x1080, display-referred, ACES-fitted tone curve, split-tone grade.
import AppKit
import CoreImage
import Foundation

struct Recipe: Codable {
    var name: String
    var source: String
    var mode: String
    var horizon: Double?          // horizon line in source pixels from the top (720-high source)
    var windows: Double?          // share of window panes lit, 0-1
    var signs: Bool?
    var wet: Bool?
    var lamps: [[Double]]?        // [x, y, radius, poolY?] in source pixels
    var exposure: Double?         // night base exposure (default 0.09)
    var seed: Int?
    var sky: Bool?                // replace the sky (night mode; default true)
    var fog: Double?              // horizon haze amount (default 0.22)
    var signReach: Double?        // how far below the horizon signs may be, as a share of the height (default 0.01)
    var aerial: Bool?             // windows and signs anywhere in the frame; the night sky is everything above the horizon
}

func fail(_ m: String) -> Never { FileHandle.standardError.write((m + "\n").data(using: .utf8)!); exit(1) }

let W = 1920, H = 1080, N = W * H
let srgbSpace = CGColorSpace(name: CGColorSpace.sRGB)!

// MARK: colour helpers
@inline(__always) func toLin(_ c: Float) -> Float { c <= 0.04045 ? c / 12.92 : powf((c + 0.055) / 1.055, 2.4) }
@inline(__always) func toSRGB(_ c: Float) -> Float { let x = max(0, min(1, c)); return x <= 0.0031308 ? x * 12.92 : 1.055 * powf(x, 1 / 2.4) - 0.055 }
@inline(__always) func smooth(_ a: Float, _ b: Float, _ x: Float) -> Float { let t = max(0, min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t) }
@inline(__always) func luma(_ r: Float, _ g: Float, _ b: Float) -> Float { 0.2126 * r + 0.7152 * g + 0.0722 * b }
@inline(__always) func aces(_ x: Float) -> Float { let v = max(0, x); return min(1, (v * (2.51 * v + 0.03)) / (v * (2.43 * v + 0.59) + 0.14)) }

func hash(_ a: Int, _ b: Int, _ c: Int) -> Float {
    var h = UInt64(bitPattern: Int64(a &* 73856093 ^ b &* 19349663 ^ c &* 83492791))
    h ^= h >> 33; h = h &* 0xff51afd7ed558ccd; h ^= h >> 33; h = h &* 0xc4ceb9fe1a85ec53; h ^= h >> 33
    return Float(h & 0xFFFFFF) / Float(0xFFFFFF)
}

func valueNoise(_ x: Float, _ y: Float, _ seed: Int) -> Float {
    let xi = Int(floor(x)), yi = Int(floor(y)); let fx = x - floor(x), fy = y - floor(y)
    let u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy)
    let a = hash(xi, yi, seed), b = hash(xi + 1, yi, seed), c = hash(xi, yi + 1, seed), d = hash(xi + 1, yi + 1, seed)
    return (a + (b - a) * u) + ((c + (d - c) * u) - (a + (b - a) * u)) * v
}

func fbm(_ x: Float, _ y: Float, _ seed: Int) -> Float {
    var s: Float = 0, a: Float = 0.5, f: Float = 1
    for o in 0..<5 { s += a * valueNoise(x * f, y * f, seed + o * 17); f *= 2; a *= 0.5 }
    return s
}

// MARK: image IO
func loadScaled(_ path: String) -> [Float] {
    guard let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: path) as CFURL, nil),
          let img = CGImageSourceCreateImageAtIndex(src, 0, nil) else { fail("cannot read \(path)") }
    let ci = CIImage(cgImage: img)
    let sx = Double(W) / Double(img.width)
    let scaled = ci.applyingFilter("CILanczosScaleTransform", parameters: ["inputScale": sx, "inputAspectRatio": (Double(H) / Double(img.height)) / sx])
    let ctx = CIContext(options: [.workingColorSpace: srgbSpace, .outputColorSpace: srgbSpace])
    var bytes = [UInt8](repeating: 0, count: N * 4)
    ctx.render(scaled, toBitmap: &bytes, rowBytes: W * 4, bounds: CGRect(x: 0, y: 0, width: W, height: H), format: .RGBA8, colorSpace: srgbSpace)
    var out = [Float](repeating: 0, count: N * 3)
    for i in 0..<N { for c in 0..<3 { out[i * 3 + c] = Float(bytes[i * 4 + c]) / 255 } }
    return out
}

func savePNG(_ rgb: [Float], _ path: String) {
    var bytes = [UInt8](repeating: 255, count: N * 4)
    for i in 0..<N { for c in 0..<3 { bytes[i * 4 + c] = UInt8(max(0, min(255, rgb[i * 3 + c] * 255 + 0.5))) } }
    let ctx = CGContext(data: &bytes, width: W, height: H, bitsPerComponent: 8, bytesPerRow: W * 4, space: srgbSpace,
                        bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
    let url = URL(fileURLWithPath: path)
    try? FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
    let dst = CGImageDestinationCreateWithURL(url as CFURL, "public.png" as CFString, 1, nil)!
    CGImageDestinationAddImage(dst, ctx.makeImage()!, nil)
    if !CGImageDestinationFinalize(dst) { fail("cannot write \(path)") }
}

// MARK: filters on float planes
/// Box blur of an interleaved buffer with `ch` channels, separable, radius r, repeated `passes` times (about Gaussian).
func boxBlur(_ a: [Float], _ w: Int, _ h: Int, _ ch: Int, rx: Int, ry: Int, passes: Int = 3) -> [Float] {
    var cur = a
    var tmp = [Float](repeating: 0, count: a.count)
    for _ in 0..<passes {
        if rx > 0 {
            for y in 0..<h { for c in 0..<ch {
                var acc: Float = 0; let n = Float(2 * rx + 1)
                for x in -rx...rx { acc += cur[(y * w + min(max(x, 0), w - 1)) * ch + c] }
                for x in 0..<w {
                    tmp[(y * w + x) * ch + c] = acc / n
                    acc += cur[(y * w + min(x + rx + 1, w - 1)) * ch + c] - cur[(y * w + max(x - rx, 0)) * ch + c]
                }
            } }
            swap(&cur, &tmp)
        }
        if ry > 0 {
            for x in 0..<w { for c in 0..<ch {
                var acc: Float = 0; let n = Float(2 * ry + 1)
                for y in -ry...ry { acc += cur[(min(max(y, 0), h - 1) * w + x) * ch + c] }
                for y in 0..<h {
                    tmp[(y * w + x) * ch + c] = acc / n
                    acc += cur[(min(y + ry + 1, h - 1) * w + x) * ch + c] - cur[(max(y - ry, 0) * w + x) * ch + c]
                }
            } }
            swap(&cur, &tmp)
        }
    }
    return cur
}

/// Flood fill over 4-neighbours from the seeds while `accept(from, to)` holds.
func flood(seeds: [Int], accept: (Int, Int) -> Bool) -> [Bool] {
    var mask = [Bool](repeating: false, count: N)
    var stack = seeds
    for s in seeds { mask[s] = true }
    while let p = stack.popLast() {
        let x = p % W, y = p / W
        for q in [x > 0 ? p - 1 : -1, x < W - 1 ? p + 1 : -1, y > 0 ? p - W : -1, y < H - 1 ? p + W : -1] where q >= 0 && !mask[q] {
            if accept(p, q) { mask[q] = true; stack.append(q) }
        }
    }
    return mask
}

/// Connected components of a mask: (pixel lists) for components of at least minArea pixels.
func components(_ m: [Bool], minArea: Int, maxArea: Int) -> [[Int]] {
    var seen = [Bool](repeating: false, count: N)
    var out: [[Int]] = []
    for s in 0..<N where m[s] && !seen[s] {
        var stack = [s]; seen[s] = true; var px: [Int] = []
        while let p = stack.popLast() {
            px.append(p)
            let x = p % W, y = p / W
            for q in [x > 0 ? p - 1 : -1, x < W - 1 ? p + 1 : -1, y > 0 ? p - W : -1, y < H - 1 ? p + W : -1] where q >= 0 && m[q] && !seen[q] {
                seen[q] = true; stack.append(q)
            }
        }
        if px.count >= minArea && px.count <= maxArea { out.append(px) }
    }
    return out
}

func feather(_ m: [Bool], _ r: Int) -> [Float] {
    boxBlur(m.map { $0 ? 1 : 0 }, W, H, 1, rx: r, ry: r, passes: 2)
}

// MARK: the treatment
func treat(_ r: Recipe, root: String, out: String) {
    let s = Float(W) / 1280 // source to working scale
    let seed = r.seed ?? 7
    let src = loadScaled(root + "/" + r.source)
    let soft = boxBlur(src, W, H, 3, rx: 2, ry: 2, passes: 1)
    let hz: Float? = r.horizon.map { Float($0) * s }
    func sat(_ i: Int) -> Float { let a = src[i * 3], b = src[i * 3 + 1], c = src[i * 3 + 2]; let mx = max(a, b, c), mn = min(a, b, c); return mx > 0 ? (mx - mn) / mx : 0 }
    func lumaAt(_ i: Int) -> Float { luma(src[i * 3], src[i * 3 + 1], src[i * 3 + 2]) }
    func near(_ p: Int, _ q: Int, _ t: Float) -> Bool {
        abs(soft[p * 3] - soft[q * 3]) < t && abs(soft[p * 3 + 1] - soft[q * 3 + 1]) < t && abs(soft[p * 3 + 2] - soft[q * 3 + 2]) < t
    }

    // Sky: flood from the top row through smooth, bright, bluish pixels, never below the horizon.
    var sky = [Bool](repeating: false, count: N)
    if r.mode == "night" && (r.sky ?? true) && r.aerial == true, let h = hz, h > 0 {
        for i in 0..<N where Float(i / W) < h - 2 { sky[i] = true }
    } else if r.mode == "night" && (r.sky ?? true), let h = hz, h > 0 {
        let seeds = (0..<W).filter { lumaAt($0) > 0.3 && src[$0 * 3 + 2] - src[$0 * 3] > 0.045 }
        sky = flood(seeds: seeds) { p, q in
            let y = Float(q / W)
            // Blue must lead red: pale plaster walls are neutral or warm, the day sky is blue to the horizon haze.
            return y < h + 2 && near(p, q, 0.02) && lumaAt(q) > 0.25 && src[q * 3 + 2] - src[q * 3] > 0.045 && src[q * 3 + 2] >= src[q * 3 + 1]
        }
    }
    let skyF = feather(sky, 2)

    // Road surface for wet reflections: flood from the bottom row through low-saturation, smooth pixels below the horizon.
    var ground = [Bool](repeating: false, count: N)
    if r.wet == true, let h = hz, h < Float(H) - 20 {
        let floor0 = max(h, 0) + 3 * s
        let seeds = ((N - W)..<N).filter { sat($0) < 0.2 && lumaAt($0) > 0.03 && lumaAt($0) < 0.62 }
        ground = flood(seeds: seeds) { p, q in
            Float(q / W) > floor0 && near(p, q, 0.05) && sat(q) < 0.24
        }
        // Close small holes (markings, cracks): dilate then erode by feathering.
        let d = feather(ground, 5)
        ground = d.map { $0 > 0.45 }
    }
    let groundF = feather(ground, 4)

    // Windows: dark, unsaturated panes clearly darker than the wall around them, above street level.
    var windowLit = [Float](repeating: 0, count: N * 3)
    var windowCount = 0, windowLitCount = 0
    if let rate = r.windows, rate > 0 {
        var l = [Float](repeating: 0, count: N)
        for i in 0..<N { l[i] = lumaAt(i) }
        let mean = boxBlur(l, W, H, 1, rx: Int(11 * s), ry: Int(11 * s), passes: 1)
        // Street views: windows above street level; aerials (horizon high in the frame or above it): anywhere.
        let limitY = r.aerial == true ? Float(H) : hz.map { $0 < 0.3 * Float(H) ? Float(H) : $0 + 0.04 * Float(H) } ?? Float(H)
        var cand = [Bool](repeating: false, count: N)
        for i in 0..<N where !sky[i] && !ground[i] && Float(i / W) < limitY {
            // Relative contrast, so it works on day renders and on dark night renders alike.
            cand[i] = mean[i] > 0.015 && (mean[i] - l[i]) / mean[i] > 0.22 && l[i] < 0.45 && sat(i) < 0.4
        }
        let comps = components(cand, minArea: Int(10 * s * s), maxArea: Int(2500 * s * s))
        let palette: [(Float, Float, Float, Float)] = [
            (1.00, 0.62, 0.30, 0.42), (0.78, 0.98, 0.92, 0.30), (1.00, 0.80, 0.55, 0.14), (0.30, 0.85, 1.00, 0.08), (0.95, 0.30, 0.80, 0.06)]
        for comp in comps {
            var x0 = W, x1 = 0, y0 = H, y1 = 0
            for p in comp { x0 = min(x0, p % W); x1 = max(x1, p % W); y0 = min(y0, p / W); y1 = max(y1, p / W) }
            let bw = x1 - x0 + 1, bh = y1 - y0 + 1
            if Float(comp.count) / Float(bw * bh) < 0.45 || bw < Int(3 * s) || bh < Int(3 * s) || bw > bh * 8 { continue }
            windowCount += 1
            if hash(x0 / 3, y0 / 3, seed) > Float(rate) { continue }
            windowLitCount += 1
            let pick = hash(x0, y0, seed + 1); var acc: Float = 0; var col = palette[0]
            for c in palette { acc += c.3; if pick <= acc { col = c; break } }
            let k = 0.9 + 1.4 * hash(y0, x0, seed + 2)
            // Light the pane's rectangle (its bounding box), brighter at the top as a ceiling light reads.
            for yy in y0...y1 { for xx in x0...x1 {
                let p = yy * W + xx
                if sky[p] { continue }
                let y = Float(yy - y0) / Float(bh)
                let g = k * (1.15 - 0.35 * y)
                windowLit[p * 3] = col.0 * g; windowLit[p * 3 + 1] = col.1 * g; windowLit[p * 3 + 2] = col.2 * g
            } }
        }
    }

    // Signs: saturated colour boards above street level glow in their own hue.
    var signLit = [Float](repeating: 0, count: N * 3)
    var signCount = 0
    if r.signs == true {
        let limitY = r.aerial == true ? Float(H) : hz.map { $0 < 0.3 * Float(H) ? Float(H) : $0 + Float(r.signReach ?? 0.01) * Float(H) } ?? Float(H)
        var cand = [Bool](repeating: false, count: N)
        for i in 0..<N where !sky[i] && !ground[i] && Float(i / W) < limitY {
            let mx = max(src[i * 3], src[i * 3 + 1], src[i * 3 + 2])
            cand[i] = sat(i) > 0.42 && mx > (r.mode == "keep" ? 0.07 : 0.25)
        }
        for comp in components(cand, minArea: Int(12 * s * s), maxArea: N / 250) {
            var x0 = W, x1 = 0, y0 = H, y1 = 0
            for p in comp { x0 = min(x0, p % W); x1 = max(x1, p % W); y0 = min(y0, p / W); y1 = max(y1, p / W) }
            if x1 - x0 + 1 < 2 * (y1 - y0 + 1) { continue } // boards are wide strips
            signCount += 1
            let fl = 1.6 + 1.6 * hash(comp[0], comp.count, seed + 3)
            for p in comp {
                let a = src[p * 3], b = src[p * 3 + 1], c = src[p * 3 + 2]; let mx = max(a, b, c, 0.01)
                // Full value, a touch more saturated, in linear light.
                for (j, v) in [a, b, c].enumerated() { signLit[p * 3 + j] = toLin(powf(v / mx, 1.4)) * fl }
            }
        }
    }

    // Base layer in linear light.
    var scene = [Float](repeating: 0, count: N * 3)
    let exposure = Float(r.exposure ?? 0.09)
    for i in 0..<N {
        var c = (toLin(src[i * 3]), toLin(src[i * 3 + 1]), toLin(src[i * 3 + 2]))
        switch r.mode {
        case "night":
            let l = luma(c.0, c.1, c.2)
            c = ((l + (c.0 - l) * 0.55) * exposure * 0.62, (l + (c.1 - l) * 0.55) * exposure * 0.80, (l + (c.2 - l) * 0.55) * exposure * 1.35)
            c.0 += 0.0016; c.1 += 0.0022; c.2 += 0.0050
        case "keep":
            c = (c.0 * 0.95, c.1 * 1.0, c.2 * 1.18)
        default: break
        }
        scene[i * 3] = c.0; scene[i * 3 + 1] = c.1; scene[i * 3 + 2] = c.2
    }

    // Night sky with city glow and underlit haze clouds.
    if r.mode == "night", let h = hz, h > 0 {
        for i in 0..<N where skyF[i] > 0.001 {
            let x = Float(i % W), y = Float(i / W)
            let t = max(0, min(1, y / max(h, 1)))
            let glow = powf(t, 3)
            var sr = 0.004 + 0.070 * glow, sg = 0.005 + 0.022 * glow, sb = 0.016 + 0.075 * glow
            let cl = smooth(0.42, 0.75, fbm(x / 260, y / 90, seed + 9)) * (0.25 + 0.75 * t)
            sr += cl * 0.050; sg += cl * 0.018; sb += cl * 0.045
            if hash(Int(x), Int(y), seed + 5) > 0.9993 && t < 0.7 { let st = 0.25 * hash(Int(y), Int(x), 3); sr += st; sg += st; sb += st * 1.1 }
            let a = skyF[i]
            scene[i * 3] = scene[i * 3] * (1 - a) + sr * a; scene[i * 3 + 1] = scene[i * 3 + 1] * (1 - a) + sg * a; scene[i * 3 + 2] = scene[i * 3 + 2] * (1 - a) + sb * a
        }
    }

    // Emission: windows, signs, lamps.
    var emit = [Float](repeating: 0, count: N * 3)
    for i in 0..<(N * 3) { emit[i] = windowLit[i] + signLit[i] }
    for lamp in r.lamps ?? [] {
        let lx = Float(lamp[0]) * s, ly = Float(lamp[1]) * s, rad = Float(lamp.count > 2 ? lamp[2] : 6) * s
        let pool = lamp.count > 3 ? Float(lamp[3]) * s : -1
        let x0 = max(0, Int(lx - rad * 12)), x1 = min(W - 1, Int(lx + rad * 12))
        let y0 = max(0, Int(ly - rad * 12)), y1 = min(H - 1, Int(max(ly + rad * 12, pool + rad * 8)))
        for y in y0...y1 { for x in x0...x1 {
            let dx = Float(x) - lx, dy = Float(y) - ly
            let d2 = (dx * dx + dy * dy) / (rad * rad)
            var e = 3.0 * expf(-d2 * 1.2) + 0.25 * expf(-d2 * 0.06)
            if pool > 0 {
                let px = (Float(x) - lx) / (rad * 9), py = (Float(y) - pool) / (rad * 2.6)
                e += 0.55 * expf(-(px * px + py * py))
            }
            let i = y * W + x
            emit[i * 3] += e * 1.0; emit[i * 3 + 1] += e * 0.80; emit[i * 3 + 2] += e * 0.62
        } }
    }
    for i in 0..<(N * 3) { scene[i] += emit[i] }

    // Wet road: mirror about the horizon, streaked vertically, over the road surface.
    if r.wet == true, let h = hz {
        var lightish = [Float](repeating: 0, count: N * 3)
        for i in 0..<(N * 3) { lightish[i] = scene[i] * 0.35 + emit[i] * 1.2 }
        let streak = boxBlur(lightish, W, H, 3, rx: Int(1.5 * s), ry: Int(9 * s), passes: 2)
        for i in 0..<N where groundF[i] > 0.001 {
            let x = i % W, y = i / W
            let my = Int(2 * h) - y
            guard my >= 0 && my < H else { continue }
            let j = my * W + x
            let dist = (Float(y) - h) / Float(H)
            let fres = 0.22 + 0.75 * expf(-dist / 0.16)
            let puddle = 0.45 + 0.55 * smooth(0.38, 0.62, fbm(Float(x) / (180 * s), Float(y) / (36 * s), seed + 11))
            let a = groundF[i] * puddle
            for c in 0..<3 { scene[i * 3 + c] = scene[i * 3 + c] * (1 - 0.35 * a) + streak[j * 3 + c] * fres * a }
        }
    }

    // Haze: a violet band at the horizon (street views), or a thin veil (aerials).
    let fogAmt = Float(r.fog ?? 0.22)
    if r.mode != "day" {
        for i in 0..<N {
            let y = Float(i / W)
            var f: Float
            if let h = hz, h > -Float(H) * 0.2, h < Float(H) * 1.2 { let d = (y - h) / (0.13 * Float(H)); f = fogAmt * expf(-d * d) } else { f = fogAmt * 0.5 * (1 - y / Float(H)) + 0.04 }
            scene[i * 3] = scene[i * 3] * (1 - f) + 0.030 * f; scene[i * 3 + 1] = scene[i * 3 + 1] * (1 - f) + 0.024 * f; scene[i * 3 + 2] = scene[i * 3 + 2] * (1 - f) + 0.060 * f
        }
    }

    // Bloom from what is bright, at quarter size.
    let qw = W / 4, qh = H / 4
    var bright = [Float](repeating: 0, count: qw * qh * 3)
    for y in 0..<qh { for x in 0..<qw { for c in 0..<3 {
        var acc: Float = 0
        for yy in 0..<4 { for xx in 0..<4 { acc += max(0, scene[((y * 4 + yy) * W + x * 4 + xx) * 3 + c] - (r.mode == "day" ? 0.9 : 0.35)) } }
        bright[(y * qw + x) * 3 + c] = acc / 16
    } } }
    let b1 = boxBlur(bright, qw, qh, 3, rx: 3, ry: 3), b2 = boxBlur(bright, qw, qh, 3, rx: 12, ry: 12)
    for y in 0..<H { for x in 0..<W {
        let fx = min(Float(x) / 4, Float(qw - 1)), fy = min(Float(y) / 4, Float(qh - 1))
        let qx = Int(fx), qy = Int(fy), q = (qy * qw + qx) * 3
        for c in 0..<3 { scene[(y * W + x) * 3 + c] += b1[q + c] * 0.55 + b2[q + c] * 0.45 }
    } }

    // Tone curve and grade, then sRGB.
    var outRGB = [Float](repeating: 0, count: N * 3)
    let pre: Float = r.mode == "day" ? 1.0 : 1.6
    for i in 0..<N {
        var c = (aces(scene[i * 3] * pre), aces(scene[i * 3 + 1] * pre), aces(scene[i * 3 + 2] * pre))
        let l = luma(c.0, c.1, c.2)
        if r.mode == "day" {
            // Restrained day grade: slight desaturation, cool shadows, warm highlights.
            let ws = 1 - smooth(0, 0.35, l), wh = smooth(0.55, 1, l)
            c = (l + (c.0 - l) * 0.82 - 0.012 * ws + 0.025 * wh, l + (c.1 - l) * 0.82 + 0.004 * ws + 0.010 * wh, l + (c.2 - l) * 0.82 + 0.020 * ws - 0.015 * wh)
        } else {
            let ws = 1 - smooth(0, 0.18, l), wm = smooth(0.05, 0.25, l) * (1 - smooth(0.4, 0.8, l)), wh = smooth(0.5, 1, l)
            c = (c.0 - 0.004 * ws + 0.018 * wm - 0.02 * wh, c.1 + 0.002 * ws - 0.006 * wm + 0.025 * wh, c.2 + 0.020 * ws + 0.022 * wm + 0.03 * wh)
        }
        outRGB[i * 3] = toSRGB(c.0); outRGB[i * 3 + 1] = toSRGB(c.1); outRGB[i * 3 + 2] = toSRGB(c.2)
    }
    savePNG(outRGB, out + "/" + r.name + ".png")
    let skyShare = Float(sky.filter { $0 }.count) / Float(N), groundShare = Float(ground.filter { $0 }.count) / Float(N)
    print("\(r.name): \(r.mode); sky \(String(format: "%.1f", skyShare * 100)) %, road \(String(format: "%.1f", groundShare * 100)) %, windows \(windowLitCount) lit of \(windowCount), signs \(signCount), lamps \((r.lamps ?? []).count)")
}

let args = CommandLine.arguments
guard args.count >= 3 else { fail("usage: treat <recipes.json> <outDir> [name ...]") }
let recipeURL = URL(fileURLWithPath: args[1])
let recipes = try! JSONDecoder().decode([Recipe].self, from: Data(contentsOf: recipeURL))
let root = ProcessInfo.processInfo.environment["DAD_ROOT"] ?? FileManager.default.currentDirectoryPath
let only = Set(args.dropFirst(3))
for r in recipes where only.isEmpty || only.contains(r.name) { treat(r, root: root, out: args[2]) }
