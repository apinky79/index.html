import XCTest
@testable import RemoteDeskLite

final class HostStoreTests: XCTestCase {
    @MainActor
    func testPersistAndReloadHosts() throws {
        let fileURL = temporaryFileURL("hosts-\(UUID().uuidString).json")

        let host = RemoteHost(
            name: "Office PC",
            address: "192.168.1.10",
            protocolType: .rdp,
            username: "admin"
        )

        let store = HostStore(fileURL: fileURL)
        store.upsert(host)

        let reloadedStore = HostStore(fileURL: fileURL)
        XCTAssertEqual(reloadedStore.hosts.count, 1)
        XCTAssertEqual(reloadedStore.hosts.first?.name, "Office PC")
        XCTAssertEqual(reloadedStore.hosts.first?.address, "192.168.1.10")

        try? FileManager.default.removeItem(at: fileURL)
    }

    @MainActor
    func testMarkConnectedSetsTimestamp() {
        let fileURL = temporaryFileURL("hosts-\(UUID().uuidString).json")

        let host = RemoteHost(
            name: "NAS",
            address: "10.0.0.5",
            protocolType: .vnc
        )

        let store = HostStore(fileURL: fileURL)
        store.upsert(host)
        store.markConnected(hostID: host.id, at: Date(timeIntervalSince1970: 42))

        let updated = store.hosts.first { $0.id == host.id }
        XCTAssertEqual(updated?.lastConnectedAt, Date(timeIntervalSince1970: 42))

        try? FileManager.default.removeItem(at: fileURL)
    }

    private func temporaryFileURL(_ filename: String) -> URL {
        URL(fileURLWithPath: NSTemporaryDirectory())
            .appendingPathComponent(filename)
    }
}
