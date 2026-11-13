// One vehicle's dynamics (E-04): springs, tyres, engine, gearbox and brakes, as forces on a rigid chassis. It holds no position of its own and knows no
// engine: each step it is told where the chassis is, asks IGroundProbe what is under each wheel, and hands forces to IForceSink. In play that is a Rigidbody
// and the world's colliders; in tests, TestBody and a made-up road. So what a vehicle does comes from one model in both.
//
// Springs. A wheel's hub rides on its spring between Droop and Bump of where it sits at rest; rates come from each axle's share of the weight and a
// natural frequency. A probe at the middle of the tyre and two along it find the road, and the hub stands where the round wheel first touches, so a kerb
// lifts it over a distance rather than in one step. Past Bump the bump stop takes the load: the body rises, the tyre does not go into the road.
//
// Tyres. Slip along and across is normalised by its peak and joined into one figure, so a tyre has one grip to share between braking and turning. Grip
// rises to the peak and falls to about 85 % of it sliding. Below about walking pace slip is measured against a fixed speed, and the force is held to what would
// stop that wheel's share of the mass in one step, so a stopped vehicle stands still without jitter.
//
// Wheel spin is implicit: the tyre is too stiff for a plain step at any sensible rate.
using System;
using Dad.Traffic;

namespace Dad.Vehicles
{
    public sealed class VehicleDynamics
    {
        const double Gravity = 9.81, AirDensity = 1.2, LowSpeed = 1.5, MaxDamperSpeed = 3, ProbeMargin = 0.1, BumpStopRate = 10, DroopSpeed = 2.5;
        const int WheelSubsteps = 4;
        /// <summary>Probes along the tyre, as shares of its radius.</summary>
        static readonly double[] Along = { 0, 0.7, -0.7 };
        // The grip curve: peak at 1, falling to sin(Shape * pi / 2) of it.
        const double Shape = 1.35, Rise = 2.3;

        public readonly HandlingSpec Spec;
        public readonly VehicleState State;
        public readonly double CentreZ, RollInertia, PitchInertia, YawInertia;

        readonly double[] rate, cornerMass;
        readonly double[] compression;
        readonly double wheelBase;
        bool turned;

        public VehicleDynamics(HandlingSpec spec, WheelSpot[] mounts, double length, double width, double height)
        {
            Spec = spec;
            var wheels = new WheelState[mounts.Length];
            double front = double.MinValue, rear = double.MaxValue;
            foreach (var m in mounts) { front = Math.Max(front, m.Z); rear = Math.Min(rear, m.Z); }
            wheelBase = front - rear;
            CentreZ = rear + spec.FrontShare * wheelBase;
            rate = new double[mounts.Length];
            cornerMass = new double[mounts.Length];
            compression = new double[mounts.Length];
            int fronts = 0, rears = 0;
            foreach (var m in mounts) { if (m.Z > (front + rear) / 2) fronts++; else rears++; }
            for (int i = 0; i < mounts.Length; i++)
            {
                var m = mounts[i];
                bool isFront = m.Z > (front + rear) / 2;
                double share = isFront ? spec.FrontShare / fronts : (1 - spec.FrontShare) / rears, hz = isFront ? spec.FrontHz : spec.RearHz;
                cornerMass[i] = spec.Mass * share;
                double w = 2 * Math.PI * hz;
                rate[i] = cornerMass[i] * w * w;
                bool driven = isFront ? spec.FrontDrive > 0 : spec.FrontDrive < 1;
                wheels[i] = new WheelState { X = m.X, RestY = m.Y, Z = m.Z, Radius = m.Y, Front = isFront, Steers = m.Steers, Driven = driven, HubY = m.Y, LastHub = m.Y };
            }
            State = new VehicleState(wheels);
            RollInertia = spec.Mass * (width * width + height * height) / 12;
            PitchInertia = 0.9 * spec.Mass * (height * height + length * length) / 12;
            YawInertia = spec.Mass * (width * width + length * length) / 12;
        }

        /// <summary>The body's load on this wheel at rest, newtons.</summary>
        public double RestLoad(int wheel) => cornerMass[wheel] * Gravity;

        /// <summary>Moves on by dt seconds: reads the ground, then hands every force on the chassis to the sink.</summary>
        public void Step(double dt, in ChassisPose pose, in DriveInput input, IGroundProbe ground, IForceSink sink)
        {
            var s = State;
            s.Speed = pose.Velocity.Dot(pose.Forward);
            // Speed over the ground in any direction: a car sliding sideways has little forward speed but is not stopped.
            overGround = (pose.Velocity - pose.Up * pose.Velocity.Dot(pose.Up)).Length;
            Pedals(dt, input);
            Steering(dt, input);

            var wheels = s.Wheels;
            for (int i = 0; i < wheels.Length; i++) Probe(dt, i, pose, ground);

            // Anti-roll: wheels of an axle, left against right.
            double frontLeft = 0, frontRight = 0, rearLeft = 0, rearRight = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                double c = wheels[i].Grounded ? compression[i] : 0;
                if (wheels[i].X == 0) continue;
                if (wheels[i].Front) { if (wheels[i].X < 0) frontLeft = c; else frontRight = c; }
                else { if (wheels[i].X < 0) rearLeft = c; else rearRight = c; }
            }

            double wheelTorque = Drivetrain(dt);
            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];
                double across = w.X == 0 ? 0 : w.Front ? Spec.FrontBar * (frontLeft - frontRight) : Spec.RearBar * (rearLeft - rearRight);
                Spring(dt, i, Math.Sign(w.X) * -across, pose, sink);
                Tyre(dt, i, wheelTorque, pose, sink);
            }

            if (Spec.TwoWheels) Balance(pose, sink);

            // Air.
            double v = pose.Velocity.Length;
            if (v > 0.1) sink.AddForceAt(pose.Velocity * (-0.5 * AirDensity * Spec.DragArea * v), pose.Centre);
            s.BrakeLamps = s.Brake > 0.05;
            s.ReverseLamps = s.Gear < 0;
            Burn(dt);
            Indicators();
        }

        // A rider keeps two wheels up by steering under the bike, which is too quick and too fine for this model of the tyres. So the balance is a torque about the line of the tyres, taking the body
        // to the lean this turn needs (the lean at which gravity and the cornering force sum along the bike), and upright when stopped, where a real rider puts a foot down. It is a force on
        // the body, so the lean shown is the lean the physics has: nothing is drawn tilted that is not.
        void Balance(in ChassisPose pose, IForceSink sink)
        {
            var s = State;
            double speed = Math.Abs(s.Speed);
            // The turn the bike is making, not the one the bars ask for: when the tyres cannot give that turn, leaning for it laid the bike down (2026-11-07). And never past the
            // lean the tyres can hold.
            double turning = s.Speed * pose.Spin.Dot(new D3(0, 1, 0));
            double most = Math.Min(Spec.LeanMost, Math.Atan(0.8 * Math.Min(Spec.FrontGrip, Spec.RearGrip)));
            double want = -Math.Clamp(Math.Atan2(turning, Gravity), -most, most) * Math.Min(1, speed / 3);
            double roll = Math.Asin(Math.Clamp(pose.Right.Y, -1, 1));
            double rate = pose.Spin.Dot(pose.Forward);
            double w = 2 * Math.PI * Spec.LeanHz;
            // The bike rolls about its tyres, not its middle: its inertia there includes the mass at the height of its centre.
            double about = RollInertia + Spec.Mass * Spec.CentreHeight * Spec.CentreHeight;
            double torque = about * (w * w * (want - roll) - 2 * w * rate);
            var f = pose.Up * (torque / 2);
            sink.AddForceAt(f, pose.Centre + pose.Right);
            sink.AddForceAt(-f, pose.Centre - pose.Right);
        }

        // A tank lasts by the work the engine does; an empty one stops it.
        void Burn(double dt)
        {
            var s = State;
            if (!s.EngineOn) return;
            double power = (0.05 + s.Throttle) * s.Rpm / Spec.RedlineRpm;
            s.Fuel = Math.Max(0, s.Fuel - power * dt / Spec.TankSeconds);
            if (s.Fuel <= 0) s.EngineOn = false;
        }

        // An indicator stays on through the turn it was set for, and goes off when the wheel comes back.
        void Indicators()
        {
            var s = State;
            double way = s.SteerAngle * s.Indicator / Spec.MaxSteer;
            if (s.Indicator == 0) { turned = false; return; }
            if (way > 0.35) turned = true;
            else if (turned && way < 0.08) { s.Indicator = 0; turned = false; }
        }

        // Throttle and brake come on over a moment, as a foot does. One pedal does for both ways: the brake pedal at a stop selects reverse.
        double overGround;

        void Pedals(double dt, in DriveInput input)
        {
            var s = State;
            double throttle = input.Throttle, brake = input.Brake;
            bool stopped = Math.Abs(s.Speed) < 0.6 && overGround < 0.6;
            if (s.Gear < 0)
            {
                if (throttle > 0.1 && stopped) s.Gear = 1;
                else (throttle, brake) = (brake, throttle);
            }
            else if (brake > 0.1 && throttle < 0.1 && stopped) { s.Gear = -1; (throttle, brake) = (brake, 0); }
            s.Throttle = Approach(s.Throttle, s.EngineOn ? throttle : 0, 6 * dt);
            s.Brake = Approach(s.Brake, brake, 8 * dt);
            s.Handbrake = input.Handbrake;
        }

        // The wheels can turn only as far as the road can hold at this speed, plus a little: past that a car ploughs and nothing more happens.
        void Steering(double dt, in DriveInput input)
        {
            var s = State;
            double v = Math.Max(Math.Abs(s.Speed), 0.5);
            double limit = Math.Min(Spec.MaxSteer, Math.Atan(wheelBase * Spec.SteerGrip / (v * v)) + Spec.SteerSlack);
            double want = Math.Clamp(input.Steer, -1, 1) * limit;
            bool returning = Math.Abs(want) < Math.Abs(s.SteerAngle) || want * s.SteerAngle < 0;
            s.SteerAngle = Approach(s.SteerAngle, want, (returning ? Spec.SteerReturn : Spec.SteerRate) * dt);
        }

        void Probe(double dt, int i, in ChassisPose pose, IGroundProbe ground)
        {
            var w = State.Wheels[i];
            w.Steer = w.Steers ? State.SteerAngle : 0;
            double sn = Math.Sin(w.Steer), cs = Math.Cos(w.Steer);
            var heading = pose.Forward * cs + pose.Right * sn;
            double top = w.RestY + Spec.Bump + w.Radius + ProbeMargin, reach = top + 1e-3 - (w.RestY - Spec.Droop - w.Radius);
            double hub = double.MinValue;
            var down = -pose.Up;
            D3 contact = default, normal = pose.Up;
            for (int k = 0; k < Along.Length; k++)
            {
                double off = Along[k] * w.Radius;
                var from = pose.ToWorld(w.X, top, w.Z) + heading * off;
                if (!ground.Cast(from, down, reach, out var hit)) continue;
                double y = top - hit.Distance + Math.Sqrt(w.Radius * w.Radius - off * off);
                if (y > hub) { hub = y; contact = from + down * hit.Distance; normal = hit.Normal.Dot(pose.Up) > 0.5 ? hit.Normal : pose.Up; }
            }
            w.WasGrounded = w.Grounded;
            w.Grounded = hub >= w.RestY - Spec.Droop;
            w.LastHub = w.HubY;
            w.HubY = w.Grounded ? hub : Math.Max(w.RestY - Spec.Droop, w.HubY - DroopSpeed * dt);
            if (w.Grounded) { w.Contact = contact; w.Normal = normal; }
            // Metres from where the spring carries nothing.
            double free = w.RestY - cornerMass[i] * Gravity / rate[i];
            compression[i] = w.HubY - free;
        }

        void Spring(double dt, int i, double across, in ChassisPose pose, IForceSink sink)
        {
            var w = State.Wheels[i];
            if (!w.Grounded) { w.Load = 0; return; }
            double rise = Math.Clamp((w.HubY - w.LastHub) / dt, -MaxDamperSpeed, MaxDamperSpeed);
            if (!w.WasGrounded) rise = 0;
            double critical = 2 * Math.Sqrt(rate[i] * cornerMass[i]);
            double load = rate[i] * compression[i] + critical * (rise > 0 ? Spec.BumpDamping : Spec.ReboundDamping) * rise + across;
            double over = w.HubY - (w.RestY + Spec.Bump);
            if (over > 0) load += rate[i] * BumpStopRate * over;
            w.Load = Math.Max(0, load);
            sink.AddForceAt(w.Normal * w.Load, w.Contact);
        }

        // Engine to wheels: the torque at each driven wheel, this step.
        double Drivetrain(double dt)
        {
            var s = State;
            double driven = 0, wheelRate = 0;
            foreach (var w in s.Wheels) if (w.Driven) { driven++; wheelRate += w.Spin; }
            if (driven == 0) return 0;
            wheelRate /= driven;
            double ratio = s.Gear < 0 ? -Spec.ReverseRatio * Spec.FinalDrive : Spec.Gears[Math.Max(s.Gear, 1) - 1] * Spec.FinalDrive;
            double fromWheels = wheelRate * ratio * 60 / (2 * Math.PI);
            // The clutch: below the road's revs the engine holds revs of its own, higher the more it is asked of.
            double launch = Spec.IdleRpm + s.Throttle * (Spec.PeakRpm * 0.7 - Spec.IdleRpm);
            double target = s.EngineOn ? Math.Max(fromWheels, launch) : 0;
            s.Rpm = Approach(s.Rpm, Math.Min(target, Spec.RedlineRpm * 1.02), 9000 * dt);
            if (s.ShiftLeft > 0) { s.ShiftLeft -= dt; return 0; }
            if (s.Gear > 0) Gearbox(fromWheels);
            if (!s.EngineOn) return 0;
            double torque = s.Throttle * Curve(s.Rpm);
            if (s.Rpm >= Spec.RedlineRpm) torque = 0;
            // Engine braking, off the throttle.
            if (s.Throttle < 0.05 && fromWheels > Spec.IdleRpm * 1.2) torque -= 0.12 * Spec.PeakTorque * Math.Min(1, s.Rpm / Spec.PeakRpm);
            return torque * ratio * 0.9 / driven;
        }

        void Gearbox(double fromWheels)
        {
            var s = State;
            int top = Spec.Gears.Length;
            double up = Spec.IdleRpm + (Spec.ShiftRpm - Spec.IdleRpm) * Math.Max(0.45, s.Throttle);
            if (s.Gear < top && fromWheels > up)
            {
                s.Gear++;
                s.ShiftLeft = Spec.ShiftTime;
                return;
            }
            if (s.Gear > 1)
            {
                double below = fromWheels * Spec.Gears[s.Gear - 2] / Spec.Gears[s.Gear - 1];
                if (below < up * 0.82) { s.Gear--; s.ShiftLeft = Spec.ShiftTime * 0.7; }
            }
        }

        double Curve(double rpm)
        {
            double x = rpm / Spec.PeakRpm;
            return Spec.PeakTorque * Math.Clamp(x < 1 ? 0.62 + 0.38 * x * (2 - x) : 1 - 0.55 * (x - 1) * (x - 1) * 4, 0.3, 1);
        }

        void Tyre(double dt, int i, double driveTorque, in ChassisPose pose, IForceSink sink)
        {
            var w = State.Wheels[i];
            var s = State;
            double r = w.Radius, inertia = Spec.WheelInertia;
            double sn = Math.Sin(w.Steer), cs = Math.Cos(w.Steer);
            var along = pose.Forward * cs + pose.Right * sn;
            along = (along - w.Normal * along.Dot(w.Normal)).Normalized;
            var across = w.Normal.Cross(along);
            double torque = w.Driven ? driveTorque : 0;
            if (Spec.Tcs && torque * w.SlipRatio > 0 && Math.Abs(w.SlipRatio) > 1.15 * Spec.PeakSlipRatio) torque *= 0.3;
            double brake = s.Brake * (w.Front ? Spec.FrontBrake : Spec.RearBrake);
            if (Spec.Abs && brake > 0 && Math.Abs(w.SlipRatio) > 1.15 * Spec.PeakSlipRatio && overGround > 2) brake *= 0.3;
            if (s.Handbrake && !w.Front) brake += Spec.Handbrake;
            if (!w.Grounded)
            {
                w.Spin = BrakeTo(w.Spin + dt * torque / inertia, dt * brake / inertia);
                w.Spin *= 1 - 0.3 * dt;
                w.Angle += w.Spin * dt;
                w.Skid = 0;
                return;
            }
            var v = pose.PointVelocity(w.Contact);
            double vx = v.Dot(along), vy = v.Dot(across);
            double grip = (w.Front ? Spec.FrontGrip : Spec.RearGrip) * (1 - Spec.LoadFade * (w.Load / Math.Max(1, RestLoad(i)) - 1));
            grip = Math.Clamp(grip, 0.5, 1.3);
            double most = grip * w.Load;
            double slipY = vy / Math.Max(Math.Abs(vx), LowSpeed) / Spec.PeakSlipAngle;
            double h = dt / WheelSubsteps, fx = 0;
            double roll = Spec.Rolling * w.Load * r;
            for (int k = 0; k < WheelSubsteps; k++)
            {
                double surface = w.Spin * r, den = Math.Max(Math.Max(Math.Abs(vx), Math.Abs(surface)), LowSpeed);
                double slipX = (surface - vx) / den / Spec.PeakSlipRatio;
                double total = Math.Sqrt(slipX * slipX + slipY * slipY);
                double f = most * Force(total);
                fx = total > 1e-9 ? f * slipX / total : 0;
                // How much the road's pull changes with the wheel's speed, for the implicit step.
                double slope = most * Shape * Rise / (Spec.PeakSlipRatio * den);
                double soft = inertia + h * r * r * slope;
                w.Spin += h * (torque - r * fx) / soft;
                w.Spin = BrakeTo(w.Spin, h * (brake + roll) / soft);
            }
            {
                double surface = w.Spin * r, den = Math.Max(Math.Max(Math.Abs(vx), Math.Abs(surface)), LowSpeed);
                double slipX = (surface - vx) / den / Spec.PeakSlipRatio;
                double total = Math.Sqrt(slipX * slipX + slipY * slipY);
                double f = most * Force(total);
                fx = total > 1e-9 ? f * slipX / total : 0;
                double fy = total > 1e-9 ? -f * slipY / total : 0;
                // No more than would stop this wheel's share of the mass in one step.
                double stopX = Math.Abs(surface - vx) * cornerMass[i] / dt, stopY = Math.Abs(vy) * cornerMass[i] / dt;
                fx = Math.Clamp(fx, -stopX, stopX);
                fy = Math.Clamp(fy, -stopY, stopY);
                w.SlipRatio = slipX * Spec.PeakSlipRatio;
                w.SlipAngle = slipY * Spec.PeakSlipAngle;
                w.Skid = Math.Clamp(total - 1, 0, 1);
                sink.AddForceAt(along * fx, w.Contact + pose.Up * (Spec.AntiDive * Spec.CentreHeight));
                sink.AddForceAt(across * fy, w.Contact + pose.Up * Spec.RollCentre);
            }
            w.Angle += w.Spin * dt;
        }

        static double Force(double slip) => Math.Sin(Shape * Math.Atan(Rise * slip));

        static double BrakeTo(double spin, double by) => Math.Abs(spin) <= by ? 0 : spin - Math.Sign(spin) * by;

        static double Approach(double from, double to, double by) => from < to ? Math.Min(to, from + by) : Math.Max(to, from - by);
    }
}
