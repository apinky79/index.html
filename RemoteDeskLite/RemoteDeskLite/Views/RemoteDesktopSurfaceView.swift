import SwiftUI

struct RemoteDesktopSurfaceView: View {
    var fluidMode: Bool

    @State private var cursorLocation: CGPoint = CGPoint(x: 0.5, y: 0.5)
    @State private var pointerActive = false

    var body: some View {
        GeometryReader { geometry in
            ZStack {
                RoundedRectangle(cornerRadius: 16, style: .continuous)
                    .fill(
                        LinearGradient(
                            colors: fluidMode ? [.cyan.opacity(0.7), .blue.opacity(0.7)] : [.gray.opacity(0.8), .black.opacity(0.85)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )

                VStack(spacing: 10) {
                    Image(systemName: fluidMode ? "speedometer" : "display")
                        .font(.system(size: 48))
                        .foregroundStyle(.white.opacity(0.85))

                    Text(fluidMode ? "Fluid Streaming Mode" : "Battery Saver Mode")
                        .font(.headline)
                        .foregroundStyle(.white)

                    Text("Drag to simulate touchpad / pointer events")
                        .font(.caption)
                        .foregroundStyle(.white.opacity(0.85))
                }

                Circle()
                    .fill(pointerActive ? .white : .white.opacity(0.7))
                    .frame(width: pointerActive ? 20 : 14, height: pointerActive ? 20 : 14)
                    .position(
                        x: max(0, min(geometry.size.width, cursorLocation.x * geometry.size.width)),
                        y: max(0, min(geometry.size.height, cursorLocation.y * geometry.size.height))
                    )
                    .shadow(radius: pointerActive ? 8 : 3)
            }
            .gesture(
                DragGesture(minimumDistance: 0)
                    .onChanged { value in
                        pointerActive = true
                        cursorLocation = CGPoint(
                            x: value.location.x / max(geometry.size.width, 1),
                            y: value.location.y / max(geometry.size.height, 1)
                        )
                    }
                    .onEnded { _ in
                        pointerActive = false
                    }
            )
        }
        .frame(height: 320)
        .padding(.horizontal)
    }
}
