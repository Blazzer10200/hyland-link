//! Live server status via the mod's status query: TCP to the game port, send `DS_STATUS`,
//! read one JSON line (ServerStatusSnapshot, PascalCase).

use std::io::{BufRead, BufReader, Read, Write};
use std::net::{TcpStream, ToSocketAddrs};
use std::time::{Duration, Instant};

use serde::Serialize;
use serde_json::Value;

const TIMEOUT: Duration = Duration::from_millis(2500);
const MAX_RESPONSE: u64 = 16 * 1024;

#[derive(Clone, Debug, Serialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ServerStatus {
    pub online: bool,
    pub name: Option<String>,
    pub description: Option<String>,
    pub players: Option<i64>,
    pub max_players: Option<i64>,
    pub ping_ms: Option<u128>,
    pub error: Option<String>,
}

fn try_query(host: &str, port: u16) -> Result<ServerStatus, String> {
    let addr = (host, port)
        .to_socket_addrs()
        .map_err(|e| format!("can't look up {host} ({e})"))?
        .next()
        .ok_or_else(|| format!("can't look up {host}"))?;
    let started = Instant::now();
    let mut stream = TcpStream::connect_timeout(&addr, TIMEOUT).map_err(|_| "no answer".to_string())?;
    let ping_ms = started.elapsed().as_millis();
    stream.set_read_timeout(Some(TIMEOUT)).map_err(|e| e.to_string())?;
    stream.set_write_timeout(Some(TIMEOUT)).map_err(|e| e.to_string())?;
    stream.write_all(b"DS_STATUS\n").map_err(|e| e.to_string())?;

    let mut line = String::new();
    BufReader::new(stream.take(MAX_RESPONSE))
        .read_line(&mut line)
        .map_err(|_| "the server didn't answer the status check".to_string())?;
    let snapshot: Value =
        serde_json::from_str(line.trim()).map_err(|_| "the server sent an unexpected status reply".to_string())?;
    let text = |key: &str| snapshot.get(key).and_then(Value::as_str).map(str::to_owned);
    let number = |key: &str| snapshot.get(key).and_then(Value::as_i64);
    Ok(ServerStatus {
        online: true,
        name: text("ServerName"),
        description: text("ServerDescription"),
        players: number("CurrentPlayers"),
        max_players: number("MaxPlayers"),
        ping_ms: Some(ping_ms),
        error: None,
    })
}

pub fn query(host: &str, port: u16) -> ServerStatus {
    try_query(host, port).unwrap_or_else(|error| ServerStatus { online: false, error: Some(error), ..Default::default() })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::net::TcpListener;

    #[test]
    fn reads_a_status_reply() {
        let listener = TcpListener::bind("127.0.0.1:0").unwrap();
        let port = listener.local_addr().unwrap().port();
        std::thread::spawn(move || {
            let (mut socket, _) = listener.accept().unwrap();
            let mut request = String::new();
            BufReader::new(socket.try_clone().unwrap()).read_line(&mut request).unwrap();
            assert_eq!(request, "DS_STATUS\n");
            socket
                .write_all(b"{\"ProtocolVersion\":1,\"ServerName\":\"Blazzer's Schedule I\",\"ServerDescription\":\"hi\",\"CurrentPlayers\":2,\"MaxPlayers\":8}\r\n")
                .unwrap();
        });
        let status = query("127.0.0.1", port);
        assert!(status.online, "{:?}", status.error);
        assert_eq!(status.name.as_deref(), Some("Blazzer's Schedule I"));
        assert_eq!((status.players, status.max_players), (Some(2), Some(8)));
    }

    #[test]
    fn reports_offline_when_nothing_listens() {
        let port = TcpListener::bind("127.0.0.1:0").unwrap().local_addr().unwrap().port();
        let status = query("127.0.0.1", port);
        assert!(!status.online);
        assert!(status.error.is_some());
    }
}
