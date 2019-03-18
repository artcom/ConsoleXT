using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using ArtCom.Logging;

public class LogRotationTests {
	private static string _folderPath {
		get {
			return Application.persistentDataPath + "/logging_test";
		}
	}

	private static string _filenameFormat {
		get {
			return "/log {0:" + LogFormat.TimeStampFormatISO8601 + "}.txt";
		}
	}


	[Test]
	public void LogRotatorFileCountDetection() {
		ClearTestLoggingPath();
		var config = new TextLogOutputConfig {
			// we're generating 15 files from 1900 - thus the 15th file is 1914
			LoggingPath = _folderPath + "/log 1994-01-01T00-00-00.txt",
			LogRotate = true,
			LogRotateMaxFiles = 10
		};
		var rotator = new LogRotator(config);
		for(var i = 0; i < 15; i++) {
			var filePath = _folderPath + string.Format(_filenameFormat, new DateTime(1980 + i, 1, 1));
			var fs = File.Create(filePath);
			fs.Close();
			// File creation dates are only per seconds, so we're skipping a bit on time (giving us a future).
			File.SetCreationTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
			File.SetLastAccessTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
			File.SetLastWriteTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
		}
		
		rotator.RefreshObservedFiles();
		rotator.PerformLogCountTrimming();

		
		var files = Directory.GetFiles(_folderPath);
		Assert.AreEqual(10, files.Length);
		Assert.IsTrue(files.Contains(config.LoggingPath));
	}

	private static void ClearTestLoggingPath() {
		Directory.CreateDirectory(_folderPath);
		foreach(var file in Directory.GetFiles(_folderPath)) {
			File.Delete(file);
		}
	}

	[Test]
	public void LogRotatorFileAgeDetection() {
		ClearTestLoggingPath();
		var config = new TextLogOutputConfig {
			LoggingPath = _folderPath + "/log 1994-01-01T00-00-00.txt",
			LogRotate = true,
			// a year.
			LogRotateMaxAge = new TimeSpan(365, 0, 0, 0)
		};
		var rotator = new LogRotator(config);
		for(var i = 0; i < 15; i++) {
			var filePath = _folderPath + string.Format(_filenameFormat, new DateTime(1980 + i, 1, 1));
			var fs = File.Create(filePath);
			fs.Close();
			File.SetCreationTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
			File.SetLastAccessTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
			File.SetLastWriteTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
		}
		File.SetCreationTimeUtc(config.LoggingPath, DateTime.UtcNow);
		File.SetLastAccessTimeUtc(config.LoggingPath, DateTime.UtcNow);
		File.SetLastWriteTimeUtc(config.LoggingPath, DateTime.UtcNow);
		rotator.RefreshObservedFiles();
		rotator.PerformLogTimeSpanTrimming();
		var files = Directory.GetFiles(_folderPath);
		Assert.AreEqual(1, files.Length);
		Assert.AreEqual(config.LoggingPath, files[0]);
	}

	[Test]
	public void LogRotatorFileSizeDetection() {
		ClearTestLoggingPath();
		var config = new TextLogOutputConfig {
			LoggingPath = _folderPath + "/log 1994-01-01T00-00-00.txt",
			LogRotate = true,
			LogRotateMaxSize = 5 * 1024
		};
		var rotator = new LogRotator(config);
		for(var i = 0; i < 15; i++) {
			var filePath = _folderPath + string.Format(_filenameFormat, new DateTime(1980 + i, 1, 1));
			var fs = File.Create(filePath);
			// dump 1024 bytes into the file, so we should have 15kbyte of actual log data,
			// disregarding drive chunk size
			for(var j = 0; j < 1024; j++) {
				fs.WriteByte(0x00);
			}
			fs.Flush();
			fs.Close();
			File.SetCreationTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
			File.SetLastAccessTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
			File.SetLastWriteTimeUtc(filePath, new DateTime(1980 + i, 1, 1));
		}
		rotator.RefreshObservedFiles();
		rotator.PerformLogSizeTrimming();
		var files = Directory.GetFiles(_folderPath);
		Assert.AreEqual(5, files.Length);
		Assert.IsTrue(files.Contains(config.LoggingPath));
	}

}
