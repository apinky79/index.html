import Foundation

final class MockConnectionEngine: RemoteConnectionEngine {
    private var runner: Task<Void, Never>?

    func connect(to host: RemoteHost, credentials: SessionCredentials) -> AsyncStream<ConnectionEvent> {
        disconnect()

        return AsyncStream { continuation in
            runner = Task {
                continuation.yield(.state(.resolving))
                try? await Task.sleep(for: .milliseconds(400))

                continuation.yield(.state(.connecting))
                try? await Task.sleep(for: .milliseconds(600))

                continuation.yield(.state(.authenticating))
                try? await Task.sleep(for: .milliseconds(700))

                guard !credentials.password.isEmpty else {
                    continuation.yield(.state(.failed("Password is required for \(host.protocolType.rawValue).")))
                    continuation.finish()
                    return
                }

                continuation.yield(.state(.connected))

                while !Task.isCancelled {
                    let latency = Int.random(in: 18...120)
                    let bandwidth = Int.random(in: 1_200...8_500)
                    let fps = Int.random(in: 18...60)

                    continuation.yield(
                        .telemetry(
                            latencyMs: latency,
                            bandwidthKbps: bandwidth,
                            framesPerSecond: fps
                        )
                    )

                    try? await Task.sleep(for: .seconds(1))
                }

                continuation.yield(.state(.disconnected))
                continuation.finish()
            }

            continuation.onTermination = { [weak self] _ in
                self?.runner?.cancel()
                self?.runner = nil
            }
        }
    }

    func disconnect() {
        runner?.cancel()
        runner = nil
    }
}
