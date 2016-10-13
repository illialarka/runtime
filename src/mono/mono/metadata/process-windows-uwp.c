/*
 * process-windows-uwp.c: UWP process support for Mono.
 *
 * Copyright 2016 Microsoft
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
*/
#include <config.h>
#include <glib.h>

#if G_HAVE_API_SUPPORT(HAVE_UWP_WINAPI_SUPPORT)
#include <Windows.h>
#include "mono/metadata/process-windows-internals.h"

gboolean
mono_process_win_enum_processes (DWORD *pids, DWORD count, DWORD *needed)
{
	g_unsupported_api ("EnumProcesses");
	*needed = 0;
	SetLastError (ERROR_NOT_SUPPORTED);

	return FALSE;
}

HANDLE
ves_icall_System_Diagnostics_Process_GetProcess_internal (guint32 pid)
{
	HANDLE handle;

	/* GetCurrentProcess returns a pseudo-handle, so use
	 * OpenProcess instead
	 */
	handle = OpenProcess (PROCESS_ALL_ACCESS, TRUE, pid);
	if (handle == NULL)
		/* FIXME: Throw an exception */
		return NULL;
	return handle;
}

void
process_get_fileversion (MonoObject *filever, gunichar2 *filename, MonoError *error)
{
	g_unsupported_api ("GetFileVersionInfoSize, GetFileVersionInfo, VerQueryValue, VerLanguageName");

	mono_error_init (error);
	mono_error_set_not_supported (error, G_UNSUPPORTED_API, "GetFileVersionInfoSize, GetFileVersionInfo, VerQueryValue, VerLanguageName");

	SetLastError (ERROR_NOT_SUPPORTED);
}

MonoObject*
process_add_module (HANDLE process, HMODULE mod, gunichar2 *filename, gunichar2 *modulename, MonoClass *proc_class, MonoError *error)
{
	g_unsupported_api ("GetModuleInformation");

	mono_error_init (error);
	mono_error_set_not_supported (error, G_UNSUPPORTED_API, "GetModuleInformation");

	SetLastError (ERROR_NOT_SUPPORTED);

	return NULL;
}

MonoArray *
ves_icall_System_Diagnostics_Process_GetModules_internal (MonoObject *this_obj, HANDLE process)
{
	MonoError mono_error;
	mono_error_init (&mono_error);

	g_unsupported_api ("EnumProcessModules, GetModuleBaseName, GetModuleFileNameEx");

	mono_error_set_not_supported (&mono_error, G_UNSUPPORTED_API, "EnumProcessModules, GetModuleBaseName, GetModuleFileNameEx");
	mono_error_set_pending_exception (&mono_error);

	SetLastError (ERROR_NOT_SUPPORTED);

	return NULL;
}

MonoBoolean
ves_icall_System_Diagnostics_Process_ShellExecuteEx_internal (MonoProcessStartInfo *proc_start_info, MonoProcInfo *process_info)
{
	MonoError mono_error;
	mono_error_init (&mono_error);

	g_unsupported_api ("ShellExecuteEx");

	mono_error_set_not_supported (&mono_error, G_UNSUPPORTED_API, "ShellExecuteEx");
	mono_error_set_pending_exception (&mono_error);

	process_info->pid = (guint32)(-ERROR_NOT_SUPPORTED);
	SetLastError (ERROR_NOT_SUPPORTED);

	return FALSE;
}

MonoString *
ves_icall_System_Diagnostics_Process_ProcessName_internal (HANDLE process)
{
	MonoError error;
	MonoString *string;
	gunichar2 name[MAX_PATH];
	guint32 len;

	len = GetModuleFileName (NULL, name, G_N_ELEMENTS (name));
	if (len == 0)
		return NULL;

	string = mono_string_new_utf16_checked (mono_domain_get (), name, len, &error);
	if (!mono_error_ok (&error))
		mono_error_set_pending_exception (&error);

	return string;
}

void
mono_process_init_startup_info (HANDLE stdin_handle, HANDLE stdout_handle, HANDLE stderr_handle, STARTUPINFO *startinfo)
{
	startinfo->cb = sizeof(STARTUPINFO);
	startinfo->dwFlags = 0;
	startinfo->hStdInput = INVALID_HANDLE_VALUE;
	startinfo->hStdOutput = INVALID_HANDLE_VALUE;
	startinfo->hStdError = INVALID_HANDLE_VALUE;
	return;
}

gboolean
mono_process_create_process (MonoProcInfo *mono_process_info, gunichar2 *shell_path, MonoString *cmd, guint32 creation_flags,
			     gchar *env_vars, gunichar2 *dir, STARTUPINFO *start_info, PROCESS_INFORMATION *process_info)
{
	MonoError	mono_error;
	gchar		*api_name = "";

	if (mono_process_info->username) {
		api_name = "CreateProcessWithLogonW";
	} else {
		api_name = "CreateProcess";
	}

	memset (&process_info, 0, sizeof (PROCESS_INFORMATION));
	g_unsupported_api (api_name);

	mono_error_init (&mono_error);
	mono_error_set_not_supported (&mono_error, G_UNSUPPORTED_API, api_name);
	mono_error_set_pending_exception (&mono_error);

	SetLastError (ERROR_NOT_SUPPORTED);

	return FALSE;
}

#else /* G_HAVE_API_SUPPORT(HAVE_UWP_WINAPI_SUPPORT) */

#ifdef _MSC_VER
// Quiet Visual Studio linker warning, LNK4221, in cases when this source file intentional ends up empty.
void __mono_win32_process_windows_uwp_quiet_lnk4221(void) {}
#endif
#endif /* G_HAVE_API_SUPPORT(HAVE_UWP_WINAPI_SUPPORT) */
