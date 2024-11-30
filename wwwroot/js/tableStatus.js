"use strict";

const activeTimers = new Map(); // 활성 타이머 관리를 위한 Map

var connection = new signalR.HubConnectionBuilder().withUrl("/tablehub", {
    skipNegotiation: true,
    transport: signalR.HttpTransportType.WebSockets
}).build();

connection.on("ReceiveWebSocketMessage", function (tableId, message) {
    console.log(`Received message for table ${tableId}:`, message);
    updateAllTables(); // 모든 테이블 업데이트
});

connection.on("UpdateConnectionStatus", function (data) {
    console.log("Received connection status update:", data);
    if (data && data.connectedTables) {
        data.connectedTables.forEach(tableId => updateTableUI(tableId, true));
    }
});

connection.on("UpdateTableStatus", function (tableId, isActive) {
    console.log(`Received table status update: Table ${tableId} is ${isActive ? 'active' : 'inactive'}`);
    updateTableUI(tableId, isActive);
});

connection.on("GameStarted", function (tableId) {
    console.log(`Game started on table ${tableId}`);
    updateTableUI(tableId, true);
    getGameList(tableId);
});

connection.on("GameEnded", function (tableId) {
    console.log(`Game ended on table ${tableId}`);
    updateTableUI(tableId, true);
    getGameList(tableId);
});

connection.on("ForceStartGame", function (tableId) {
    console.log(`Force start game signal received for table ${tableId}`);
});

connection.on("ForceEndGame", function (tableId) {
    console.log(`Force end game signal received for table ${tableId}`);
});

connection.start().then(function () {
    console.log("SignalR Connected");
    return connection.invoke("GetAllTableStatus");
}).then(function() {
    console.log("Initial table status retrieved");
    updateAllTables();
}).catch(function (err) {
    return console.error(err.toString());
});

document.addEventListener('DOMContentLoaded', updateAllTables);

function updateGameList(tableId, games) {
    // 먼저 현재 설정된 요금을 가져옵니다
    fetch('/Settings/GetFeePerMinute')
        .then(response => response.json())
        .then(data => {
            const feePerMinute = data.feePerMinute;
            updateGameListUI(tableId, games, feePerMinute);
        })
        .catch(error => {
            console.error('Error fetching fee per minute:', error);
            // 오류 시 기본값으로 진행
            updateGameListUI(tableId, games, 0.5);
        });
}

// UI 업데이트를 별도 함수로 분리
function updateGameListUI(tableId, games, feePerMinute) {
    var gameList = document.getElementById(`game-list-${tableId}`);
    if (!gameList) return;

    // 이전 타이머들 정리
    if (activeTimers.has(tableId)) {
        activeTimers.get(tableId).forEach(timer => clearInterval(timer));
        activeTimers.delete(tableId);
    }

    gameList.innerHTML = '';
    const tableTimers = new Set();

    games.forEach((game, index) => {
        var listItem = document.createElement('li');
        listItem.className = 'list-group-item';
        const timerId = `timer-${tableId}-${game.id}`;

        const isOngoing = !game.finished;
        const timerHtml = isOngoing ? 
            `<div id="${timerId}" class="alert alert-info mt-2">진행시간: 계산중...</div>` : '';

        listItem.innerHTML = `
            <strong>Game ${index + 1}</strong><br>
            시작: ${formatDate(game.start)}<br>
            종료: ${game.finished ? formatDate(game.end) : '진행 중'}<br>
            ${timerHtml}
            요금: $${game.fee.toFixed(2)}<br>
            <button class="btn btn-sm btn-primary mt-2" onclick="editGame(${game.id})">Edit</button>
            <button class="btn btn-sm btn-success mt-2 ml-2" onclick="saveGame(${game.id}, ${tableId})">Save</button>
            <button class="btn btn-sm btn-danger mt-2 ml-2" onclick="deleteGame(${game.id}, ${tableId})">Delete</button>
        `;
        gameList.appendChild(listItem);

        if (isOngoing) {
            const startTime = new Date(game.start);
            const timerElement = document.getElementById(timerId);

            function updateTimer() {
                const now = new Date();
                const diffInMinutes = Math.floor((now - startTime) / (1000 * 60));
                const diffInSeconds = Math.floor((now - startTime) / 1000);
                const hours = Math.floor(diffInSeconds / 3600);
                const minutes = Math.floor((diffInSeconds % 3600) / 60);
                const seconds = diffInSeconds % 60;

                timerElement.innerHTML = `
                    진행시간: ${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}
                    (${diffInMinutes}분, 요금: $${(diffInMinutes * feePerMinute).toFixed(2)})
                `;
            }

            updateTimer();
            const timer = setInterval(updateTimer, 1000);
            tableTimers.add(timer);
        }
    });
    
    if (tableTimers.size > 0) {
        activeTimers.set(tableId, tableTimers);
    }

    var saveAllButton = document.createElement('button');
    saveAllButton.className = 'btn btn-info mt-3';
    saveAllButton.textContent = 'Save All';
    saveAllButton.onclick = () => saveAllGames(tableId);
    gameList.appendChild(saveAllButton);
}

function getGameList(tableId) {
    fetch(`/Games/GetGameList/${tableId}`)
    .then(response => response.json())
    .then(games => {
        updateGameList(tableId, games);
    })
    .catch(error => console.error('Error:', error));
}

function formatDate(dateString) {
    return new Date(dateString).toLocaleString();
}

function updateTableUI(tableId, isActive) {
    var tableCard = document.getElementById("table-" + tableId);
    var statusText = document.getElementById("status-" + tableId);

    if (tableCard && statusText) {
        if (isActive) {
            tableCard.classList.remove("bg-light");
            tableCard.classList.add("bg-success");
            statusText.textContent = "상태: 사용 중";
        } else {
            tableCard.classList.remove("bg-success");
            tableCard.classList.add("bg-light");
            statusText.textContent = "상태: 비활성";
        }
    } else {
        console.error(`Elements for table ${tableId} not found`);
    }
}

function saveGame(gameId, tableId) {
    fetch(`/Games/SaveGame/${gameId}`, {
        method: 'POST',
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            // 전체 테이블 업데이트 대신 해당 테이블만 업데이트
            getGameList(tableId);
            // alert은 한 번만 표시
            alert('Game saved successfully.');
        } else {
            alert('Failed to save game: ' + data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function editGame(gameId) {
    window.open(`/Games/Edit/${gameId}`, 'EditGame', 'width=600,height=400');
}

function deleteGame(gameId, tableId) {
    if (confirm('Are you sure you want to delete this game?')) {
        fetch(`/Games/DeleteGame/${gameId}`, {
            method: 'POST',
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                alert('Game deleted successfully.');
                getGameList(tableId);
            } else {
                alert('Failed to delete game: ' + data.message);
            }
        })
        .catch(error => console.error('Error:', error));
    }
}


async function saveAllGames(tableId) {
    try {
        if (!confirm('Are you sure you want to move all games to records?')) {
            return;
        }

        const response = await fetch(`/Games/MoveAllGamesToRecords/${tableId}`, {
            method: 'POST'
        });

        const result = await response.json();
        
        if (result.success) {
            // 게임 목록을 바로 비우고
            const gameList = document.getElementById(`game-list-${tableId}`);
            if (gameList) {
                gameList.innerHTML = '';
                // Save All 버튼만 다시 추가
                const saveAllButton = document.createElement('button');
                saveAllButton.className = 'btn btn-info mt-3';
                saveAllButton.textContent = 'Save All';
                saveAllButton.onclick = () => saveAllGames(tableId);
                gameList.appendChild(saveAllButton);
            }
            // 한 번만 알림
            alert('All games moved to records successfully.');
        }
    } catch (error) {
        console.error('Error:', error);
        alert('Failed to move games to records');
    }
}

function startGame(tableId) {
    fetch('/Games/StartGame', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Game started:', data.game);
            updateTableUI(tableId, true);
            getGameList(tableId);
        } else {
            console.error('Failed to start game:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function endGame(tableId) {
    fetch('/Games/EndGame', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Game ended:', data.game);
            updateTableUI(tableId, false);
            getGameList(tableId);
        } else {
            console.error('Failed to end game:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function forceStartGame(tableId) {
    fetch(`/Games/ForceStartGame/${tableId}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Force start game signal sent for table:', tableId);
        } else {
            console.error('Failed to send force start game signal:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function forceEndGame(tableId) {
    fetch(`/Games/ForceEndGame/${tableId}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Force end game signal sent for table:', tableId);
        } else {
            console.error('Failed to send force end game signal:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function updateAllTables() {
    // 이미 업데이트 중인지 확인하는 플래그 추가
    if (window.isUpdating) return;
    window.isUpdating = true;
    
    const promises = [];
    for (let i = 1; i <= 12; i++) {
        promises.push(getGameList(i));
    }
    
    Promise.all(promises).finally(() => {
        window.isUpdating = false;
    });
}

// 페이지 언로드 시 모든 타이머 정리
window.addEventListener('beforeunload', () => {
    activeTimers.forEach(timers => {
        timers.forEach(timer => clearInterval(timer));
    });
});