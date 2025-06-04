-- Bảng Accounts
CREATE TABLE Accounts (
    accountID INT PRIMARY KEY,
    name NVARCHAR(100),
    avatar NVARCHAR(255),
    email NVARCHAR(255) UNIQUE,
    password NVARCHAR(255),
    role NVARCHAR(50),
    status BIT,
    createdAt DATETIME
);

-- Bảng Learners
CREATE TABLE Learners (
    accountID INT PRIMARY KEY,
    level NVARCHAR(50),
    interest NVARCHAR(255),
    FOREIGN KEY (accountID) REFERENCES Accounts(accountID)
);

-- Bảng Partners
CREATE TABLE Partners (
    accountID INT PRIMARY KEY,
    bio NVARCHAR(255),
    isApproved BIT,
    FOREIGN KEY (accountID) REFERENCES Accounts(accountID)
);

-- Bảng Admin
CREATE TABLE Admin (
    accountID INT PRIMARY KEY,
    is_approved BIT,
    FOREIGN KEY (accountID) REFERENCES Accounts(accountID)
);

-- Bảng Courses
CREATE TABLE Courses (
    courseID INT PRIMARY KEY,
    name NVARCHAR(255),
    description NVARCHAR(MAX),
    coverImage NVARCHAR(255),
    numberOfView INT,
    tuition DECIMAL(18,2),
    evaluationPoint FLOAT,
    createdBy INT,
    createdAt DATETIME,
    FOREIGN KEY (createdBy) REFERENCES Accounts(accountID)
);

-- Bảng CourseLearningOutcomes
CREATE TABLE CourseLearningOutcomes (
    outcomeID INT PRIMARY KEY,
    courseID INT,
    description NVARCHAR(500),
    FOREIGN KEY (courseID) REFERENCES Courses(courseID)
);

-- Bảng CourseVideos
CREATE TABLE CourseVideos (
    videoID INT PRIMARY KEY,
    courseID INT,
    videoLink NVARCHAR(255),
    FOREIGN KEY (courseID) REFERENCES Courses(courseID)
);

-- Bảng PurchasedCourses
CREATE TABLE PurchasedCourses (
    accountID INT,
    courseID INT,
    purchasedDate DATETIME,
    PRIMARY KEY (accountID, courseID),
    FOREIGN KEY (accountID) REFERENCES Accounts(accountID),
    FOREIGN KEY (courseID) REFERENCES Courses(courseID)
);

-- Bảng WatchedVideos
CREATE TABLE WatchedVideos (
    accountID INT,
    videoID INT,
    watchedAt DATETIME,
    PRIMARY KEY (accountID, videoID),
    FOREIGN KEY (accountID) REFERENCES Accounts(accountID),
    FOREIGN KEY (videoID) REFERENCES CourseVideos(videoID)
);
