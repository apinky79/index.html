import Foundation

enum RemoteProtocol: String, Codable, CaseIterable, Identifiable {
    case rdp = "RDP"
    case vnc = "VNC"

    var id: String { rawValue }

    var defaultPort: Int {
        switch self {
        case .rdp:
            return 3389
        case .vnc:
            return 5900
        }
    }

    var transportName: String {
        switch self {
        case .rdp:
            return "Microsoft Remote Desktop Protocol"
        case .vnc:
            return "Virtual Network Computing"
        }
    }
}

struct RemoteHost: Identifiable, Codable, Equatable {
    var id: UUID
    var name: String
    var address: String
    var port: Int
    var protocolType: RemoteProtocol
    var username: String
    var notes: String
    var autoReconnect: Bool
    var lastConnectedAt: Date?

    init(
        id: UUID = UUID(),
        name: String,
        address: String,
        port: Int? = nil,
        protocolType: RemoteProtocol,
        username: String = "",
        notes: String = "",
        autoReconnect: Bool = true,
        lastConnectedAt: Date? = nil
    ) {
        self.id = id
        self.name = name
        self.address = address
        self.port = port ?? protocolType.defaultPort
        self.protocolType = protocolType
        self.username = username
        self.notes = notes
        self.autoReconnect = autoReconnect
        self.lastConnectedAt = lastConnectedAt
    }

    var endpoint: String {
        "\(address):\(port)"
    }
}
