import Foundation

enum ConnectionState: Equatable {
    case idle
    case resolving
    case connecting
    case authenticating
    case connected
    case disconnected
    case failed(String)

    var title: String {
        switch self {
        case .idle:
            return "Idle"
        case .resolving:
            return "Resolving Host"
        case .connecting:
            return "Opening Socket"
        case .authenticating:
            return "Authenticating"
        case .connected:
            return "Connected"
        case .disconnected:
            return "Disconnected"
        case let .failed(message):
            return "Failed: \(message)"
        }
    }

    var isConnected: Bool {
        if case .connected = self {
            return true
        }
        return false
    }
}

enum ConnectionEvent: Equatable {
    case state(ConnectionState)
    case telemetry(latencyMs: Int, bandwidthKbps: Int, framesPerSecond: Int)
}

protocol RemoteConnectionEngine {
    func connect(to host: RemoteHost, credentials: SessionCredentials) -> AsyncStream<ConnectionEvent>
    func disconnect()
}
