#!/bin/bash
# Script to make a PLGX file for KeeOTP2 on Linux with Mono

# See: https://stackoverflow.com/questions/59895/how-to-get-the-source-directory-of-a-bash-script-from-within-the-script-itself
# Note: you can't refactor this out: its at the top of every script so the scripts can find their includes.
SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" >/dev/null 2>&1 && pwd )"

# atexit handler
ATEXIT=()

function atexit() {
  ATEXIT+=( "$*" )
}

function _atexit_handler() {
  local EXPR
  for (( idx=${#ATEXIT[@]}-1 ; idx>=0 ; idx-- )); do
    EXPR="${ATEXIT[idx]}"
    echo "atexit: $EXPR" 1>&2
    eval "$EXPR"
  done
}

trap _atexit_handler EXIT

function log() {
  echo "$*" 1>&2
}

function fatal() {
  echo "$*" 1>&2
  exit 1
}

if ! keepass2=$(command -v keepass2); then
    fatal "Failed to find a keepass2 executable on PATH"
fi

if [ ! -x "${keepass2}" ]; then
    fatal "Keepass2 command is not executable"
fi

if ! rsync="$(command -v rsync)"; then
    fatal "rsync command is not usable"
fi

if [ ! -x "${rsync}" ]; then
    fatal "rsync command is not usable"
fi

log "Make working directory"
if ! tmp_dir="$(mktemp -t -d makeplgx.XXXXXXXX)"; then
    fatal "Failed to create temporary working directory"
fi
atexit "[ ! -z ${tmp_dir} ] && rm -rf ${tmp_dir}"
log "Temporary directory: ${tmp_dir}"

work_dir="${tmp_dir}/KeeOtp2"
if ! mkdir -p "${work_dir}"; then
  fatal "Could not create ${work_dir}"
fi

log "Working directory: ${work_dir}"

log "Copy the source code to the working directory"
if ! rsync -a -W --exclude obj --exclude bin --exclude obj  "${SCRIPT_DIR}/KeeOtp2/" "${work_dir}/"; then
    fatal "rsync call failed"
fi

log "Copy dependency licenses for main build"
if ! rsync -a -W  "${SCRIPT_DIR}/Dependencies" "${work_dir}"; then
    fatal "rsync call failed"
fi

csproj_file="${work_dir}/KeeOtp2.csproj"

log "Copying library dependencies"
# Extract the list of packages from the CSPROJ file. This is not the right way to do it, but I'm not going to spend
# an hour remembering XPath when I can be sure the pattern will be similar anyway:
while read -r package_path; do

    package_name=$(echo "${package_path}" | cut -d '\' -f3 | rev | cut -d'.' -f4- | rev)
    package_dest="${work_dir}/Dependencies/${package_name}"
    package_src=$(echo "${package_path}" | cut -d '\' -f2- | tr \\\\ '/')
    package_dll="$(basename "${package_src}")"
    package_dest_csproj="$(realpath -s --relative-to="${work_dir}" "${package_dest}/${package_dll}" | tr '/' \\\\ )"

    if ! mkdir -p "${package_dest}"; then
        fatal "Could not create package destination directory: ${package_dest}"
    fi

    log "Copying package: ${package_name}"
    if ! cp -t "${package_dest}" "${package_src}" ; then
        fatal "Error copying $package_src to $package_dest"
    fi

    log "Replacing path in destination csproj..."
    if ! sed -i "s#${package_path//\\/\\\\}#${package_dest_csproj//\\/\\\\}#g" "${csproj_file}"; then
        fatal "Path update of project file failed: ${csproj_file}"
    fi

done < <(grep HintPath "${SCRIPT_DIR}/KeeOtp2/KeeOtp2.csproj" | sed -r 's#\s+<HintPath>(.*)</HintPath>#\1#' | grep 'packages')

log "Edit Resources.resx paths"
sed -i 's#\.\.\\\.\.\\Dependencies\\#..\\Dependencies\\#g' "${SCRIPT_DIR}/KeeOtp2/Properties/Resources.resx"

log "Building PLGX file"
if ! $keepass2  --plgx-create "${work_dir}" --plgx-prereq-net:4.7.2; then
  fatal "Failed to build PLGX file"
fi

if ! cp -f -t "${SCRIPT_DIR}" "${tmp_dir}/KeeOtp2.plgx"; then
  fatal "Failed copying generated PLGX back to script directory"
fi
