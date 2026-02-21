import Foundation

@MainActor
final class HostStore: ObservableObject {
    @Published private(set) var hosts: [RemoteHost] = []

    private let fileURL: URL
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder

    init(fileURL: URL? = nil) {
        self.fileURL = fileURL ?? Self.defaultFileURL()

        encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601

        decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601

        load()
    }

    func sortedHosts() -> [RemoteHost] {
        hosts.sorted { lhs, rhs in
            switch (lhs.lastConnectedAt, rhs.lastConnectedAt) {
            case let (left?, right?):
                return left > right
            case (.some, .none):
                return true
            case (.none, .some):
                return false
            case (.none, .none):
                return lhs.name.localizedCaseInsensitiveCompare(rhs.name) == .orderedAscending
            }
        }
    }

    func upsert(_ host: RemoteHost) {
        if let index = hosts.firstIndex(where: { $0.id == host.id }) {
            hosts[index] = host
        } else {
            hosts.append(host)
        }
        persist()
    }

    func delete(_ host: RemoteHost) {
        hosts.removeAll { $0.id == host.id }
        persist()
    }

    func markConnected(hostID: UUID, at timestamp: Date = Date()) {
        guard let index = hosts.firstIndex(where: { $0.id == hostID }) else {
            return
        }
        hosts[index].lastConnectedAt = timestamp
        persist()
    }

    private func load() {
        guard FileManager.default.fileExists(atPath: fileURL.path) else {
            hosts = []
            return
        }

        do {
            let data = try Data(contentsOf: fileURL)
            hosts = try decoder.decode([RemoteHost].self, from: data)
        } catch {
            hosts = []
        }
    }

    private func persist() {
        do {
            try FileManager.default.createDirectory(
                at: fileURL.deletingLastPathComponent(),
                withIntermediateDirectories: true
            )
            let data = try encoder.encode(hosts)
            try data.write(to: fileURL, options: [.atomic])
        } catch {
            assertionFailure("Unable to persist hosts: \(error)")
        }
    }

    private static func defaultFileURL() -> URL {
        if let documentsDirectory = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first {
            return documentsDirectory.appendingPathComponent("hosts.json")
        }

        return URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("hosts.json")
    }
}
