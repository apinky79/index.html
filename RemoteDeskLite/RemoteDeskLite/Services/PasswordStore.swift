import Foundation
import Security

protocol PasswordStore {
    func password(for hostID: UUID) -> String?
    func setPassword(_ password: String?, for hostID: UUID) throws
}

enum PasswordStoreError: LocalizedError {
    case unexpectedStatus(OSStatus)
    case stringEncodingFailed

    var errorDescription: String? {
        switch self {
        case let .unexpectedStatus(status):
            return "Keychain operation failed with status code \(status)."
        case .stringEncodingFailed:
            return "Unable to convert password to UTF-8 data."
        }
    }
}

final class KeychainPasswordStore: PasswordStore {
    private let service = "com.example.RemoteDeskLite.credentials"

    func password(for hostID: UUID) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: hostID.uuidString,
            kSecMatchLimit as String: kSecMatchLimitOne,
            kSecReturnData as String: true
        ]

        var result: AnyObject?
        let status = SecItemCopyMatching(query as CFDictionary, &result)

        guard status != errSecItemNotFound else {
            return nil
        }

        guard status == errSecSuccess, let data = result as? Data else {
            return nil
        }

        return String(data: data, encoding: .utf8)
    }

    func setPassword(_ password: String?, for hostID: UUID) throws {
        let account = hostID.uuidString
        let baseQuery: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]

        if let password {
            guard let data = password.data(using: .utf8) else {
                throw PasswordStoreError.stringEncodingFailed
            }

            let attributes: [String: Any] = [kSecValueData as String: data]
            let updateStatus = SecItemUpdate(baseQuery as CFDictionary, attributes as CFDictionary)

            if updateStatus == errSecSuccess {
                return
            }

            if updateStatus == errSecItemNotFound {
                var addQuery = baseQuery
                addQuery[kSecValueData as String] = data
                let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
                guard addStatus == errSecSuccess else {
                    throw PasswordStoreError.unexpectedStatus(addStatus)
                }
                return
            }

            throw PasswordStoreError.unexpectedStatus(updateStatus)
        } else {
            let deleteStatus = SecItemDelete(baseQuery as CFDictionary)
            guard deleteStatus == errSecSuccess || deleteStatus == errSecItemNotFound else {
                throw PasswordStoreError.unexpectedStatus(deleteStatus)
            }
        }
    }
}

final class InMemoryPasswordStore: PasswordStore {
    private var storage: [UUID: String] = [:]

    func password(for hostID: UUID) -> String? {
        storage[hostID]
    }

    func setPassword(_ password: String?, for hostID: UUID) throws {
        storage[hostID] = password
    }
}
